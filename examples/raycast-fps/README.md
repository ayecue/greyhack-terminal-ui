# Raycast FPS for Grey Hack

A 2.5D raycasting first-person shooter for Grey Hack, inspired by classic games like Wolfenstein 3D. Uses the greyhack-terminal-canvas mod for graphics.

## Features

- **Real-time raycasting** - Classic 2.5D rendering engine
- **Multiple levels** - 3 different maps to clear
- **Enemy AI** - Enemies track and attack the player
- **Combat system** - Shoot enemies, manage ammo, reload
- **HUD** - Health bar, ammo counter, score, minimap
- **High score** - Persistent best score tracking

## Architecture

This game uses the same **dual-terminal architecture** as the snake-game example:

1. **Display Mode** (`fps display`): Runs the game loop - updates physics, AI, and renders graphics
2. **Input Mode** (`fps input`): Captures keyboard input and writes to a file

### Why This Architecture?

In Grey Hack, `user_input()` pauses script execution. By separating concerns:
- The **display terminal** runs the continuous game loop with real-time updates
- The **input terminal** handles key capture asynchronously
- Input is queued to prevent lost keypresses during fast input

## Usage

1. Open two terminals in Grey Hack
2. In the first terminal, run: `fps display`
3. In the second terminal, run: `fps input`
4. Press Enter or W to start the game!

## Controls

| Key | Action |
|-----|--------|
| W | Move forward |
| S | Move backward |
| A | Strafe left |
| D | Strafe right |
| Left Arrow / J | Turn left |
| Right Arrow / L | Turn right |
| Space / F | Shoot |
| R | Reload / Restart |
| Enter | Start / Continue |
| Q | Quit |

## Gameplay

- Navigate through the level using WASD for movement
- Turn with arrow keys or J/L
- Shoot enemies before they get too close
- Enemies will chase you when you're in range
- Clear all enemies to complete the level
- Watch your health - enemies deal damage when close!

## Technical Details

### Raycasting

The renderer uses the DDA (Digital Differential Analyzer) algorithm to cast rays from the player's viewpoint. Each vertical strip of the screen corresponds to one ray, determining:
- Distance to walls (for wall height calculation)
- Wall type (for color/texture)
- Side hit (N/S vs E/W for shading)

### Sprite Rendering

Enemies are drawn as billboard sprites:
- Sorted by distance (far to near)
- Depth buffer prevents drawing behind walls
- Size scales inversely with distance

## Requirements

- Grey Hack game
- greyhack-terminal-canvas mod (BepInEx plugin)

## Building

```bash
# Use greybel to compile
greybel build --target fps.src --out build/
```

## State Files

- `/root/fps_input.state` - Input commands queue
- `/root/fps_highscore.dat` - Persistent high score
