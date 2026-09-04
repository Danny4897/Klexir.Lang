const vscode = require("vscode");
const path = require("path");
const os = require("os");

const TUTORIAL_SAMPLES = [
  { id: "01", file: "01-basics.klx" },
  { id: "02", file: "02-functions.klx" },
  { id: "03", file: "03-control-flow.klx" },
  { id: "04", file: "04-option-result.klx" },
  { id: "05", file: "05-strings-lists.klx" },
  { id: "06", file: "06-records.klx" },
  { id: "07", file: "07-unions.klx" },
  { id: "08", file: "08-recursion.klx" },
  { id: "09", file: "09-plugins.klx" },
  { id: "10", file: "10-layered-architecture.klx" },
];

/** @type {vscode.Uri} */
let extensionUri;

/** @param {vscode.ExtensionContext} context */
function activate(context) {
  extensionUri = context.extensionUri;

  context.subscriptions.push(
    vscode.commands.registerCommand("klexir.runFile", runActiveFile)
  );

  for (const sample of TUTORIAL_SAMPLES) {
    context.subscriptions.push(
      vscode.commands.registerCommand(`klexir.openSample.${sample.id}`, () =>
        openSample(sample.file)
      )
    );
  }
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
 * Opens a tutorial sample so it's editable and runnable — copies it out of the extension's own
 * install directory the first time (into the open workspace, or the user's home as a fallback), so
 * edits are the user's own and survive an extension update/reinstall.
 */
async function openSample(fileName) {
  const source = vscode.Uri.joinPath(extensionUri, "samples", fileName);
  const targetDir = tutorialDir();
  const target = vscode.Uri.joinPath(targetDir, fileName);

  const alreadyCopied = await fileExists(target);
  if (!alreadyCopied) {
    await vscode.workspace.fs.createDirectory(targetDir);
    const content = await vscode.workspace.fs.readFile(source);
    await vscode.workspace.fs.writeFile(target, content);
  }

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
