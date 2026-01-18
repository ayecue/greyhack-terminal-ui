# TODO App for Grey Hack

A terminal-based todo list application for Grey Hack, using the greyhack-terminal-canvas mod for a graphical UI.

## Features

- Add, edit, and delete todos
- Mark todos as complete/incomplete
- Navigate with keyboard
- Nice graphical UI rendered via terminal canvas
- Persistent storage

## Architecture

This app uses a **dual-terminal architecture** due to GreyScript's `user_input` blocking behavior:

1. **Input Mode** (`todo input`): Handles keyboard input and updates the app state
2. **Display Mode** (`todo display`): Renders the graphical UI from state file

### Why Two Modes?

In Grey Hack, `user_input()` pauses script execution until the user provides input. This means a single script cannot simultaneously:
- Wait for user input
- Continuously render/update the display

By splitting into two terminals:
- The input terminal waits for user commands
- The display terminal continuously renders the current state

## Usage

1. Open two terminals in Grey Hack
2. In the first terminal, run: `todo display`
3. In the second terminal, run: `todo input`

## Controls

| Key | Action |
|-----|--------|
| W / Up Arrow | Navigate up |
| S / Down Arrow | Navigate down |
| E / Enter | Select/Toggle complete |
| A | Add new todo |
| D | Delete selected todo |
| X | Edit selected todo |
| Q | Quit |

## Requirements

- Grey Hack game
- greyhack-terminal-canvas mod (BepInEx plugin)

## Building

```bash
# Use greybel to compile
greybel build --target todo.src --out build/
```

## State File

The app stores state in `/root/todo_render.state` for communication between input and display modes.

Todos are persisted to `/root/todos.dat`.
