# TaskRecorder Design Spec

**Date:** 2026-07-07
**Tech:** WPF + .NET 8, JSON persistence
**Path:** `D:\tools\TaskRecorder`

---

## Overview

A minimal, tree-structured task list desktop app. Independent tasks stack vertically; parent-child task chains extend horizontally with connecting lines. Completed tasks get a strikethrough and become read-only. The app minimizes to the system tray instead of closing.

---

## Password Gate

On startup, before showing the main window, a small password dialog appears:

- A simple `Window` with a `PasswordBox` and "确认" button.
- Hardcoded password: `huyang`. Stored as a `const string` in code — change requires recompilation.
- Wrong password → shake animation + clear input, retry.
- Correct password → dialog closes, main window opens.
- 3 consecutive failures → app exits (anti-brute-force).
- The password dialog also has a tray-like behavior: closing the dialog (✕) exits the app.

---

## Data Model

```csharp
public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ObservableCollection<TaskItem> SubTasks { get; set; } = new();
}
```

- `Id` is a GUID for stable identity across serialization and structural moves.
- `CreatedAt` is set when the task is first created; `CompletedAt` is set when marked complete, `null` when uncompleted.
- `SubTasks` is `ObservableCollection<TaskItem>` for live UI binding.
- Infinite nesting: each `TaskItem` holds its own sub-task collection.

A top-level list is serialized as `List<TaskItem>` in `tasks.json` at the app root.

---

## Persistence

- **Format:** JSON (`System.Text.Json`)
- **File:** `tasks.json` in the app directory (`D:\tools\TaskRecorder\tasks.json`)
- **Save trigger:** After every create / edit / complete toggle / delete operation.
- **Load:** On app startup.
- **Error handling:**
  - Missing file → start with empty list, create file on first save.
  - Corrupt JSON → show message box, rename bad file to `tasks.json.bak`, start fresh.

---

## UI Layout

```
┌────────────────────────────────────────────────────┐
│  Window: TaskRecorder                       [_][☐][✕]│
├────────────────────────────────────────────────────┤
│                                                    │
│  ┌─ Task row (ItemsControl + ScrollViewer) ──────┐│
│  │ ○─Task A────□─Sub A1────□─Sub A2───────────►  ││  ← horizontal scroll
│  │                                                ││
│  │ ○─Task B────□─Sub B1────□─SubB1-child──────►  ││
│  │                                                ││
│  │ ○─Task C (last, auto-scrolled to)              ││
│  │                                                ││
│  │ ...                                            ││
│  └────────────────────────────────────────────────┘│
│                                                    │
│  ┌──────────────────────────────────────────────┐ │
│  │ [                                      Add]   │ │  ← fixed at bottom
│  └──────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────┘
```

### Component tree

- **Window:** `ShowInTaskbar = false`, `WindowState.Minimized` → Hide + show tray icon.
- **Main area:** `ItemsControl` bound to top-level `List<TaskItem>`, inside a `ScrollViewer` (vertical). Auto-scrolls to bottom on add and on startup.
- **Each top-level row:** A horizontal `ItemsControl` (recursively flattening the task tree depth-first into a flat list of nodes for that chain) inside a horizontal `ScrollViewer`.
- **Each node:** Task text (editable on double-click) + connecting line to the next node.
  - Line: a horizontal `Line` / `Rectangle` between adjacent nodes in the template.
  - Completed state: `TextDecorations.Strikethrough` + grey foreground.
- **Bottom bar:** `TextBox` + "Add" `Button`, docked at window bottom.
- **Tray icon:** `System.Windows.Forms.NotifyIcon` with context menu "Exit".

---

## Interactions

### Add a top-level task
1. Type in bottom TextBox.
2. Press Enter or click Add.
3. New `TaskItem` appended to list end → `ScrollViewer.ScrollToBottom()`.

### Right-click context menu (on any task node)

Ordered top to bottom:

| Menu item              | Behavior                                                      |
|------------------------|---------------------------------------------------------------|
| **Edit**               | Open a small dialog with a TextBox pre-filled with task title. Enter to confirm, title updates. |
| **Complete / Uncomplete** | Toggle `IsCompleted`. Completed → strikethrough + grey + disables Edit / Add Subtask. Uncomplete reverses. |
| **Add Subtask**        | Open dialog, enter title, new `TaskItem` appended to `SubTasks`. Appears to the right in the horizontal chain. |
| **Delete**             | Confirmation dialog → remove from parent's `SubTasks` (or top-level list). Entire subtree deleted. |

### Quick actions
- **Double-click** on task text → same as Edit.
- **Window close (✕)** → `Hide()` + tray icon visible (does NOT exit).
- **Tray icon → Exit** → `Application.Current.Shutdown()` (saves first on `OnClosing`).

### Inline editing alternative discarded
Edit via dialog (not inline textbox-swap) was chosen for simplicity — avoids focus/layout shift issues.

---

## Visual design rules

- **Connecting lines:** Each node renders a horizontal line (`─`) to its right neighbor. The last node in a chain omits the line.
- **Time display (below task text in each node):**
  - `CreatedAt` always shown in small grey text: `07-07 14:30`
  - If `IsCompleted` and `CompletedAt` is set: `07-07 14:30  ✓ 07-08 09:15`
  - Combined format: small grey text, completion time appears with a checkmark after the creation time.
- **Completion state:**
  - Text: strikethrough, grey foreground.
  - Edit disabled (menu item greyed out).
  - Add Subtask disabled.
  - Complete/Uncomplete and Delete still available.
- **Empty state:** If no tasks, the main area shows a subtle placeholder text ("No tasks yet. Type below to get started.") and the bottom input bar still visible.

---

## Error handling

| Scenario                                 | Handling                                                     |
|------------------------------------------|--------------------------------------------------------------|
| `tasks.json` missing (first launch)      | Start empty. No error.                                       |
| `tasks.json` corrupt                     | `MessageBox` warning → rename to `tasks.json.bak` → start empty. |
| Empty/whitespace-only title              | Prevent add (both top-level and subtask). Input retains focus. |
| Add subtask dialog closed without input  | No action.                                                   |
| Delete confirmation → user clicks Cancel | No action.                                                   |
| File write fails (permissions, disk full)| `MessageBox` error → operation still applied in memory.      |
| Window close (✕ button)                  | `Hide()` + show tray notification (first time).              |
| Tray → Exit                              | `OnClosing` saves, then `Shutdown()`.                        |

---

## Project structure

```
D:\tools\TaskRecorder\
├── TaskRecorder.csproj
├── tasks.json                   (runtime data)
├── Models\
│   └── TaskItem.cs
├── ViewModels\
│   └── MainViewModel.cs
├── Views\
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── PasswordWindow.xaml
│   ├── PasswordWindow.xaml.cs
│   └── TaskNode.xaml             (DataTemplate for a single node)
├── Services\
│   └── TaskStorageService.cs     (JSON load/save)
├── App.xaml
└── App.xaml.cs                   (tray icon setup, startup)
```

### Key NuGet packages
- None beyond .NET 8 SDK defaults. JSON handled by `System.Text.Json`.

---

## MVVM Notes

- `MainViewModel` exposes `ObservableCollection<TaskItem> Tasks` (top-level list).
- Each `TaskItem` is its own model; tree is recursively bound.
- Commands (Add, Edit, Complete, AddSubtask, Delete) are `ICommand` or code-behind shortcuts. Given app simplicity, a mix of `Button.Command` bindings and code-behind event handlers is acceptable — no need for a full MVVM framework.
- Tray icon lives in `App.xaml.cs` (application-level concern).
