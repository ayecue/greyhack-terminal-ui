# Snake Game for Grey Hack

A classic Snake game for Grey Hack, using the greyhack-terminal-canvas mod for graphics.

## Features

- Classic snake gameplay with **continuous movement**
- Growing snake when eating food
- Score tracking with high score
- Increasing speed as you progress
- Game over and restart functionality
- Nice retro graphics

## Architecture

This game uses a **dual-terminal architecture** with a twist for continuous gameplay:

1. **Display Mode** (`snake display`): Runs the game loop - updates game state and renders graphics
2. **Input Mode** (`snake input`): Captures keyboard input and writes to a file

### Why This Architecture?

In Grey Hack, `user_input()` pauses script execution until the user provides input. Traditional snake games need the snake to move continuously without waiting for input.

By flipping the typical architecture:
- The **display terminal** runs the game loop with `wait()` for timing, reading input from a file
- The **input terminal** captures key presses and writes them to a shared file
- The snake moves automatically at a fixed tick rate
- Your direction input is read without blocking the game!

## Usage

1. Open two terminals in Grey Hack
2. In the first terminal, run: `snake display`
3. In the second terminal, run: `snake input`
4. Press any direction key (WASD) to start the game!

## Controls

| Key | Action |
|-----|--------|
| W / Up Arrow | Move up |
| A / Left Arrow | Move left |
| S / Down Arrow | Move down |
| D / Right Arrow | Move right |
| R | Restart game |
| Q | Quit |

## Requirements

- Grey Hack game
- greyhack-terminal-canvas mod (BepInEx plugin)

## Building

```bash
# Use greybel to compile
greybel build --target snake.src --out build/
```

## State Files

- `/root/snake_input.state` - Input commands from input terminal to display terminal
- `/root/snake_highscore.dat` - Persistent high score
