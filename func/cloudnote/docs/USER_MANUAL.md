# CloudNote – User Manual

## 1. Install

- **Windows**: download `CloudNote.exe` (and the `data/` folder next to
  it) from the release zip. Double-click to launch.
- **Android**: install the signed APK; allow the system browser to open
  GitHub during sign-in.

## 2. First launch

1. Tap **Continue with GitHub**.
2. The app shows an 8-character code and opens
   `https://github.com/login/device` in your browser.
3. Sign in to GitHub if necessary, enter the code, click **Authorize**.
4. CloudNote returns to the home screen automatically.

## 3. Create a notebook

1. On the home screen tap **Create GitHub notebook**.
2. Choose a name (e.g. `my-notes`).
3. CloudNote creates a *private* repository on GitHub and clones it to
   your local Documents folder (`Documents/CloudNote/<name>`).

## 4. Writing notes

- Tap the **+** button to start a new note.
- Type a title at the top; the body uses standard Markdown.
- Use the **eye / pencil** button in the toolbar to toggle preview.
- Use the **clipboard** button to paste smart content from the system
  clipboard. Text, formatted HTML (e.g. copied from a web page), and
  images (when a clipboard plugin is wired in — see Developer Guide) all
  land in the editor as markdown.
- Use the **disk** button to save.

### Supported markdown

Standard CommonMark + GitHub-flavoured extensions: tables, fenced code
blocks, task lists, autolinks, headings, blockquotes, inline images
(`![alt](path.png)`) and links.

## 5. Sync

- The **circular arrow** icon in the home toolbar performs **manual
  sync** (pull → commit changes → push).
- Open **Settings → Sync** to:
  - toggle **Auto sync** on/off
  - choose the **interval** (1, 5, 10, 15, 30, 60 minutes)

If sync fails (e.g. network down or merge conflict), the status bar shows
the error. Fix the issue and tap manual sync again.

## 6. Settings

- **Theme**: System / Light / Dark
- **Font family**: any Google Fonts family bundled with the app
- **Font size**: 0.8× – 2.0×
- **Paragraph spacing**: 1.0 – 2.5 line height
- **Font color**: a small palette plus custom (extensible)
- **Sync**: see above
- **Account**: shows GitHub username; **Sign out** clears the token

## 7. Where are my files?

Everywhere — that is the point. The Git repo is the source of truth:

- Locally: `Documents/CloudNote/<repo-name>/`
- Remotely: `https://github.com/<your-user>/<repo-name>` (private)

You can edit notes with any other markdown editor; the next sync will
pick up the changes.

## 8. Troubleshooting

| Problem                              | Try                                         |
| ------------------------------------ | ------------------------------------------- |
| "Sync failed: …"                     | Check network, then tap manual sync         |
| Images don't show                    | Make sure the path is relative to repo root |
| Sign-in code expired                 | Tap **Continue with GitHub** again          |
| "git not found" on Windows           | Install Git for Windows and re-launch       |
