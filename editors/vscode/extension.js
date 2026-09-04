const vscode = require("vscode");
const path = require("path");
const os = require("os");

const LESSONS = [
  { id: "01", file: "01-basics.klx", md: "01-basics.md", title: "Tipi primitivi", subtitle: "Int, Bool, String" },
  { id: "02", file: "02-functions.klx", md: "02-functions.md", title: "Funzioni e closures", subtitle: "fun, currying" },
  { id: "03", file: "03-control-flow.klx", md: "03-control-flow.md", title: "Controllo di flusso", subtitle: "if/then/else, confronti" },
  { id: "04", file: "04-option-result.klx", md: "04-option-result.md", title: "Option e Result", subtitle: "railway-oriented" },
  { id: "05", file: "05-strings-lists.klx", md: "05-strings-lists.md", title: "String e List", subtitle: "map/filter/fold" },
  { id: "06", file: "06-records.klx", md: "06-records.md", title: "record", subtitle: "tipi prodotto" },
  { id: "07", file: "07-unions.klx", md: "07-unions.md", title: "union", subtitle: "tipi somma, match esaustivo" },
  { id: "08", file: "08-recursion.klx", md: "08-recursion.md", title: "Ricorsione", subtitle: "let rec" },
  { id: "09", file: "09-plugins.klx", md: "09-plugins.md", title: "Plugin", subtitle: "capacita' native opt-in" },
  { id: "10", file: "10-layered-architecture.klx", md: "10-layered-architecture.md", title: "Tutto insieme", subtitle: "controller/service/repository" },
  { id: "11", file: "11-validation-pipeline.klx", md: "11-validation-pipeline.md", title: "Pipeline di validazione", subtitle: "piu' funzioni atomiche, stile MonadicSharp" },
  { id: "12", file: "12-mvp-model.klx", md: "12-mvp-model.md", title: "MVP 1/6: Modella il dominio", subtitle: "record + union del risultato" },
  { id: "13", file: "13-mvp-repository.klx", md: "13-mvp-repository.md", title: "MVP 2/6: Repository", subtitle: "una responsabilita', un motivo per cambiare" },
  { id: "14", file: "14-mvp-validators.klx", md: "14-mvp-validators.md", title: "MVP 3/6: Validatori atomici", subtitle: "firme identiche, componibili" },
  { id: "15", file: "15-mvp-service.klx", md: "15-mvp-service.md", title: "MVP 4/6: Service", subtitle: "orchestrazione via andThen" },
  { id: "16", file: "16-mvp-controller.klx", md: "16-mvp-controller.md", title: "MVP 5/6: Controller + plugin", subtitle: "Result -> risposta, timestamp reale" },
  { id: "17", file: "17-mvp-complete.klx", md: "17-mvp-complete.md", title: "MVP 6/6: Programma completo", subtitle: "checklist di clean code" },
];

const COMPLETED_KEY = "klexir.completedLessons";

/** @type {vscode.Uri} */
let extensionUri;

/** @param {vscode.ExtensionContext} context */
function activate(context) {
  extensionUri = context.extensionUri;

  const lessonsProvider = new KlexirLessonsProvider(context);
  context.subscriptions.push(
    vscode.window.registerTreeDataProvider("klexirTutorialLessons", lessonsProvider)
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("klexir.runFile", runActiveFile),
    vscode.commands.registerCommand("klexir.openLesson", (lesson) =>
      openLesson(lessonsProvider, lesson)
    ),
    vscode.commands.registerCommand("klexir.resetTutorialProgress", () =>
      lessonsProvider.resetProgress()
    )
  );

  for (const lesson of LESSONS) {
    context.subscriptions.push(
      vscode.commands.registerCommand(`klexir.openSample.${lesson.id}`, () =>
        openSample(lesson.file)
      )
    );
  }
}

/**
 * Tutor-style tree: one entry per lesson, a checkmark once it's been opened. Selecting a lesson opens
 * its runnable sample in the main editor group and the matching lesson text as a Markdown preview beside
 * it — read on one side, write and run on the other, at any time from the sidebar, not a one-shot wizard.
 */
class KlexirLessonsProvider {
  constructor(context) {
    this.context = context;
    this._onDidChangeTreeData = new vscode.EventEmitter();
    this.onDidChangeTreeData = this._onDidChangeTreeData.event;
  }

  refresh() {
    this._onDidChangeTreeData.fire();
  }

  getTreeItem(lesson) {
    const item = new vscode.TreeItem(lesson.title, vscode.TreeItemCollapsibleState.None);
    item.description = lesson.subtitle;
    item.iconPath = new vscode.ThemeIcon(this.isCompleted(lesson.id) ? "pass-filled" : "circle-large-outline");
    item.command = { command: "klexir.openLesson", title: "Apri lezione", arguments: [lesson] };
    item.contextValue = "klexirLesson";
    return item;
  }

  getChildren() {
    return LESSONS;
  }

  isCompleted(id) {
    return this.context.workspaceState.get(COMPLETED_KEY, []).includes(id);
  }

  markCompleted(id) {
    const done = this.context.workspaceState.get(COMPLETED_KEY, []);
    if (!done.includes(id)) {
      this.context.workspaceState.update(COMPLETED_KEY, [...done, id]);
      this.refresh();
    }
  }

  resetProgress() {
    this.context.workspaceState.update(COMPLETED_KEY, []);
    this.refresh();
  }
}

async function openLesson(provider, lesson) {
  const sampleUri = await copySampleIfNeeded(lesson.file);
  await vscode.window.showTextDocument(sampleUri, { viewColumn: vscode.ViewColumn.One, preview: false });

  const mdUri = vscode.Uri.joinPath(extensionUri, "walkthrough", lesson.md);
  await vscode.commands.executeCommand("markdown.showPreviewToSide", mdUri);

  provider.markCompleted(lesson.id);
}

async function runActiveFile() {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    vscode.window.showErrorMessage("Klexir: no active editor.");
    return;
  }

  if (editor.document.languageId !== "klexir") {
    vscode.window.showErrorMessage("Klexir: the active file isn't a .klx file.");
    return;
  }

  if (editor.document.isDirty) {
    await editor.document.save();
  }

  const cliProjectPath = await resolveCliProjectPath();
  if (!cliProjectPath) {
    return;
  }

  const filePath = editor.document.uri.fsPath;
  const pluginArgs = vscode.workspace
    .getConfiguration("klexir")
    .get("plugins", [])
    .map((name) => `--plugin=${name}`)
    .join(" ");

  const terminal = getOrCreateTerminal();
  terminal.show(true);
  terminal.sendText(
    `dotnet run --project "${cliProjectPath}" -- run ${pluginArgs} "${filePath}"`.replace(/\s+/g, " ")
  );
}

/**
 * Copies a bundled tutorial sample out of the extension's own install directory into the open workspace
 * (or the user's home as a fallback) the first time it's opened, so edits are the user's own and survive
 * an extension update/reinstall — returns the copy's Uri either way.
 */
async function copySampleIfNeeded(fileName) {
  const source = vscode.Uri.joinPath(extensionUri, "samples", fileName);
  const targetDir = tutorialDir();
  const target = vscode.Uri.joinPath(targetDir, fileName);

  if (!(await fileExists(target))) {
    await vscode.workspace.fs.createDirectory(targetDir);
    const content = await vscode.workspace.fs.readFile(source);
    await vscode.workspace.fs.writeFile(target, content);
  }

  return target;
}

async function openSample(fileName) {
  const target = await copySampleIfNeeded(fileName);
  await vscode.window.showTextDocument(target, { preview: false });
}

function tutorialDir() {
  const folders = vscode.workspace.workspaceFolders;
  const base = folders && folders.length > 0 ? folders[0].uri : vscode.Uri.file(os.homedir());
  return vscode.Uri.joinPath(base, "klexir-tutorial");
}

async function fileExists(uri) {
  try {
    await vscode.workspace.fs.stat(uri);
    return true;
  } catch {
    return false;
  }
}

async function resolveCliProjectPath() {
  const configured = vscode.workspace
    .getConfiguration("klexir")
    .get("cliProjectPath", "");

  if (configured && configured.trim().length > 0) {
    return configured;
  }

  const matches = await vscode.workspace.findFiles(
    "**/Klexir.Cli.csproj",
    "**/{bin,obj}/**",
    5
  );

  if (matches.length === 1) {
    return matches[0].fsPath;
  }

  if (matches.length > 1) {
    const pick = await vscode.window.showQuickPick(
      matches.map((m) => ({ label: path.basename(path.dirname(m.fsPath)), description: m.fsPath, path: m.fsPath })),
      { placeHolder: "Multiple Klexir.Cli.csproj found — pick one" }
    );
    return pick ? pick.path : undefined;
  }

  vscode.window.showErrorMessage(
    "Klexir: couldn't find Klexir.Cli.csproj in this workspace. Set 'klexir.cliProjectPath' in Settings."
  );
  return undefined;
}

let sharedTerminal;

function getOrCreateTerminal() {
  if (!sharedTerminal || sharedTerminal.exitStatus !== undefined) {
    sharedTerminal = vscode.window.createTerminal("Klexir");
  }
  return sharedTerminal;
}

function deactivate() {}

module.exports = { activate, deactivate };
