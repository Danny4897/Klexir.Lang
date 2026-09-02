const vscode = require("vscode");
const path = require("path");

/** @param {vscode.ExtensionContext} context */
function activate(context) {
  context.subscriptions.push(
    vscode.commands.registerCommand("klexir.runFile", runActiveFile)
  );
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
  const terminal = getOrCreateTerminal();
  terminal.show(true);
  terminal.sendText(
    `dotnet run --project "${cliProjectPath}" -- run "${filePath}"`
  );
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
