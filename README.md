# Grey Hack Terminal UI

A BepInEx mod for Grey Hack that adds a programmable pixel canvas controlled from GreyScript via simple `#UI{ ... }` blocks embedded in `print` statements.

The canvas is rendered in a separate Unity window and driven by a lightweight scripting VM with persistent state per terminal.

---

## Features

- Pixel canvas windows automatically associated with Grey Hack terminals
- Drawing primitives: pixels, lines, rectangles, circles, and text
- Small scripting language with:
  - Variables and arithmetic
  - Conditionals (`if / else if / else`)
  - `while` loops
  - Built‑in math and utility functions
- Persistent VM context per terminal (state survives across `print` calls)
- One VM per terminal; old executions are stopped before new ones start
- Security & abuse protection:
    - Limits on variables, string length, loop iterations, execution time
    - Cooldown on resizing the canvas

While the initial implementation focuses on enabling rich UI via the canvas, the same scripting VM and intrinsic system can be extended to drive other client‑side features (for example, playing sounds, triggering visual effects, or exposing additional in‑game controls).

---

## Requirements

- Grey Hack (Steam)
- BepInEx 5 (recommended) or BepInEx 6 for Grey Hack
- .NET 6 SDK (only if you want to build from source)

---

## Installation

1. Install BepInEx 5 for Grey Hack (follow the BepInEx docs).
2. Download the compiled DLL for this mod (GreyHackTerminalUI5).
3. Copy the DLL into your BepInEx plugins folder, for example:  
   `[Grey Hack folder]/BepInEx/plugins/GreyHackTerminalUI5.dll`
4. Start Grey Hack.  
   The mod will log its startup in the BepInEx console.

For BepInEx 6, use the corresponding BepInEx 6 build of the plugin and place it in the appropriate plugins folder.

---

## Basic Usage

You control the canvas from GreyScript by printing special `#UI{ ... }` blocks:

- The text inside `#UI{ ... }` is **not** printed to the terminal.
- Instead, it is parsed and executed by the mod.
- Each terminal gets its own canvas window and its own persistent VM context.

### Minimal Example

```greyscript
print("#UI{
    Canvas.setSize(200, 150)
    Canvas.setTitle(""Hello Canvas"")
    Canvas.show()
    Canvas.clear(""black"")
    Canvas.fillRect(""red"", 10, 10, 50, 50)
    Canvas.render()
}")
```

---

## Scripting Language Overview

The script inside `#UI{ ... }` is a small language implemented by the mod.

### Variables

```greyscript
print("#UI{
    var x = 10
    var y = 20
    var name = ""Hello""
    x = x + 5
}")
```

### Math

```greyscript
print("#UI{
    var result = (10 + 5) * 2 / 3
    var angle = sin(3.14159)
    var r = floor(random() * 100)
}")
```

### Conditionals

```greyscript
print("#UI{
    if x > 10 then
        Canvas.fillRect(""red"", x, y, 10, 10)
    else if x > 5 then
        Canvas.fillRect(""yellow"", x, y, 10, 10)
    else
        Canvas.fillRect(""green"", x, y, 10, 10)
    end if
}")
```

### While Loops

```greyscript
print("#UI{
    var i = 0
    while i < 10 do
        Canvas.setPixel(""white"", i * 10, 50)
        i = i + 1
    end while

    Canvas.render()
}")
```

### Persistent State

Variables persist per terminal between separate `print` calls:

```greyscript
// First call – initialize and increment
print("#UI{
    if not hasInContext(""frameCount"") then
        var frameCount = 0
    end if
    frameCount = frameCount + 1
}")

// Later call – reuse the same variable
print("#UI{
    Canvas.clear()
    Canvas.drawText(""white"", 10, 10, toString(frameCount))
    Canvas.render()
}")
```

---

## Canvas API

### Window & Canvas Control

- `Canvas.show()`  
- `Canvas.hide()`  
- `Canvas.setSize(w, h)`  
- `Canvas.setTitle(title)`  
- `Canvas.clear()`  
- `Canvas.clear(color)`  

Example:

```greyscript
print("#UI{
    Canvas.setSize(320, 240)
    Canvas.setTitle(""My Game"")
    Canvas.show()
    Canvas.clear(""blue"")
    Canvas.render()
}")
```

### Drawing Primitives

- `Canvas.setPixel(color, x, y)`
- `Canvas.drawLine(color, x1, y1, x2, y2)`
- `Canvas.drawRect(color, x, y, w, h)`
- `Canvas.fillRect(color, x, y, w, h)`
- `Canvas.drawCircle(color, x, y, r)`
- `Canvas.fillCircle(color, x, y, r)`
- `Canvas.drawText(color, x, y, text)`
- `Canvas.drawText(color, x, y, text, size)`
- `Canvas.render()` — apply all drawing changes

Resolution accessors:

- `Canvas.width`
- `Canvas.height`

---

## Colors

Supported formats:

- Named: `"red"`, `"green"`, `"blue"`, `"white"`, `"black"`, `"yellow"`, `"cyan"`, `"magenta"`, `"gray"`, `"orange"`, `"purple"`, `"pink"`, `"brown"`, `"lime"`, `"navy"`, `"teal"`, `"olive"`, `"maroon"`, `"silver"`, `"gold"`
- Hex: `"#FF0000"`, `"#00FF00"`, `"#0000FF"`
- Integer RGB: `"255, 0, 0"` or `"255, 128, 0"`
- Float RGB 0–1: `"1.0, 0.5, 0.0"`

---

## Built‑in Functions

- `print(msg)` — logs to BepInEx console (for debugging)
- `hasInContext(name)` — does a variable exist in the VM context?
- `typeof(val)` — type name as string
- `toNumber(val)` — convert to number
- `toString(val)` — convert to string

Math and random:

- `floor(n)`, `ceil(n)`, `round(n)`
- `abs(n)`
- `min(a, b)`, `max(a, b)`
- `sin(n)`, `cos(n)`
- `random()` — random in [0, 1)
- `randomRange(min, max)` — random in [min, max)

---

## Animation Pattern (Recommended)

For smooth animation, **drive the animation from GreyScript** with a loop that repeatedly prints a `#UI{ ... }` block. Do not busy‑loop inside the UI script itself.

Example: simple bouncing ball (GIF‑style):

```greyscript
// Initialize canvas and state
print("#UI{
    if not hasInContext(""initialized"") then
        var initialized = true
        Canvas.setSize(300, 200)
        Canvas.setTitle(""Bouncing Ball"")
        Canvas.show()

        var ballX = 150
        var ballY = 100
        var velX = 3
        var velY = 2
        var radius = 15
    end if
}")

// Animation loop in GreyScript
running = true
while running
    print("#UI{
        ballX = ballX + velX
        ballY = ballY + velY

        if ballX - radius < 0 or ballX + radius > 300 then
            velX = velX * -1
        end if

        if ballY - radius < 0 or ballY + radius > 200 then
            velY = velY * -1
        end if

        Canvas.clear(""#000020"")
        Canvas.fillCircle(""red"", ballX, ballY, radius)
        Canvas.render()
    }")

    // Control frame rate (~60 FPS)
    wait(0.016)
end while
```

---

## Example: DOOM‑Style Corridor

```greyscript
// Initialize DOOM‑like view
print("#UI{
    if not hasInContext(""doomInit"") then
        var doomInit = true
        Canvas.setSize(320, 200)
        Canvas.setTitle(""DOOM - E1M1"")
        Canvas.show()

        var walk = 0
        var health = 100
        var ammo = 50
        var enemyDist = 150
    end if
}")

// Animation loop
running = true
while running
    print("#UI{
        walk = walk + 1

        Canvas.clear(""#000000"")
        Canvas.fillRect(""#3a3a3a"", 0, 0, 320, 80)
        Canvas.fillRect(""#2a1a0a"", 0, 120, 320, 80)

        var wallLeft = 80 + floor(sin(walk * 0.05) * 5)
        var wallRight = 240 + floor(sin(walk * 0.05) * 5)

        Canvas.fillRect(""#4a2a1a"", wallLeft, 60, 15, 80)
        Canvas.fillRect(""#4a2a1a"", wallRight, 60, 15, 80)

        enemyDist = enemyDist - 1
        if enemyDist < 50 then
            enemyDist = 150
        end if
        var enemySize = floor(80 - enemyDist / 3)
        var ex = 160 - enemySize / 2
        var ey = 100 - enemySize / 2
        Canvas.fillRect(""#8a0a0a"", ex, ey, enemySize, enemySize)

        var bob = floor(sin(walk * 0.2) * 3)
        var wy = 140 + bob
        Canvas.fillRect(""#5a5a5a"", 140, wy, 40, 50)

        Canvas.fillRect(""#1a1a1a"", 0, 185, 320, 15)
        Canvas.drawText(""#00ff00"", 10, 188, ""HEALTH: "" + health + ""%"", 10)
        Canvas.drawText(""#ffaa00"", 120, 188, ""AMMO: "" + ammo, 10)

        Canvas.render()
    }")

    wait(0.033) // ~30 FPS
end while
```

---

## Sound API Reference

The `Sound` object provides methods for creating and managing multiple named sound instances with MIDI-based melodies.

### Sound Manager Methods

#### `Sound.create(name)`

Creates a new named sound instance. Returns a sound instance object that can be used to add notes and control playback.

- `name` (string): Unique identifier for this sound instance

```greyscript
print("#UI{
    welcome = Sound.create(""welcome"")
    gameOver = Sound.create(""gameOver"")
}")
```

**Note:** If a sound with the same name already exists, it will return the existing instance.

#### `Sound.get(name)`

Retrieves an existing named sound instance. Throws an error if the sound doesn't exist.

- `name` (string): Name of the sound instance to retrieve

```greyscript
print("#UI{
    welcome = Sound.get(""welcome"")
    welcome.play()
}")
```

#### `Sound.destroy(name)`

Destroys a named sound instance and frees its resources.

- `name` (string): Name of the sound instance to destroy

```greyscript
print("#UI{
    Sound.destroy(""welcome"")
}")
```

### Sound Instance Methods

Once you have a sound instance (from `Sound.create()` or `Sound.get()`), you can use these methods:

#### `instance.addNote(pitch, duration [, velocity])`

Adds a MIDI note to this sound instance's buffer.

- `pitch` (int): MIDI note number (0-127). Middle C = 60, one octave up = 72, one octave down = 48
- `duration` (float): How long to play the note in seconds
- `velocity` (float, optional): Volume/intensity of the note (0.0-1.0, default 0.7)

```greyscript
print("#UI{
    melody = Sound.create(""melody"")
    // Play middle C for half a second
    melody.addNote(60, 0.5)
    
    // Play E with custom volume
    melody.addNote(64, 0.5, 0.9)
}")
```

**MIDI Note Reference:**
- 60 = Middle C (C4)
- 61 = C# / Db
- 62 = D
- 63 = D# / Eb
- 64 = E
- 65 = F
- 66 = F# / Gb
- 67 = G
- 68 = G# / Ab
- 69 = A (440 Hz)
- 70 = A# / Bb
- 71 = B
- 72 = C (one octave up)

#### `instance.play()`

Starts playing all the notes in this instance's buffer. Notes play sequentially in the order they were added.

```greyscript
print("#UI{
    melody = Sound.get(""melody"")
    melody.play()
}")
```

#### `instance.stop()`

Stops this sound instance immediately.

```greyscript
print("#UI{
    melody = Sound.get(""melody"")
    melody.stop()
}")
```

#### `instance.clear()`

Clears all notes from this sound instance's buffer and stops playback.

```greyscript
print("#UI{
    melody = Sound.get(""melody"")
    melody.clear()
}")
```

#### `instance.isPlaying`

Read-only property that returns `true` if this sound instance is currently playing, `false` otherwise.

```greyscript
print("#UI{
    melody = Sound.get(""melody"")
    if melody.isPlaying then
        print(""Still playing..."")
    end if
}")
```

### Sound Examples

#### Multiple Sound Effects

```greyscript
print("#UI{
    // Create different sounds for different events
    welcome = Sound.create(""welcome"")
    welcome.addNote(60, 0.3)  // C
    welcome.addNote(64, 0.3)  // E
    welcome.addNote(67, 0.4)  // G
    
    error = Sound.create(""error"")
    error.addNote(65, 0.2, 0.9)  // F
    error.addNote(60, 0.4, 0.8)  // C (lower)
    
    // Play welcome sound
    welcome.play()
}")
```

#### Simple Melody

```greyscript
print("#UI{
    melody = Sound.create(""melody"")
    melody.clear()
    melody.addNote(60, 0.4)  // C
    melody.addNote(62, 0.4)  // D
    melody.addNote(64, 0.4)  // E
    melody.addNote(65, 0.4)  // F
    melody.addNote(67, 0.4)  // G
    melody.play()
}")
```

#### Game Over Sound

```greyscript
print("#UI{
    gameOver = Sound.create(""gameOver"")
    gameOver.clear()
    gameOver.addNote(67, 0.3, 0.8)  // G
    gameOver.addNote(64, 0.3, 0.7)  // E
    gameOver.addNote(60, 0.6, 0.9)  // C (longer, louder)
    gameOver.play()
}")
```

#### Sound with Animation

```greyscript
// In your game loop
if playerScored then
    print("#UI{
        // Play success sound
        success = Sound.get(""success"")
        success.clear()
        success.addNote(72, 0.15, 0.8)
        success.addNote(76, 0.15, 0.9)
        success.addNote(79, 0.3, 1.0)
        success.play()
        
        // Update UI
        Canvas.drawText(""white"", 10, 10, ""Score: "" + score)
        Canvas.render()
    }")
end if
```

#### Managing Multiple Sounds

```greyscript
print("#UI{
    // Create background music
    bgMusic = Sound.create(""background"")
    bgMusic.addNote(60, 0.5)
    bgMusic.addNote(64, 0.5)
    bgMusic.addNote(67, 0.5)
    bgMusic.play()
    
    // Later, stop background music
    if needsSilence then
        bgMusic = Sound.get(""background"")
        bgMusic.stop()
    end if
    
    // Clean up when done
    Sound.destroy(""background"")
}")
```

---

## Security & Limits

To keep things safe and responsive, the VM enforces several limits:

- **Maximum sounds per terminal**: 100 sound instances (prevents resource exhaustion)
- **Maximum notes per sound instance**: 1000 notes (prevents memory abuse)
- Each terminal can have multiple independent sound instances
- Sounds play sequentially (no polyphony)
- Only simple sine wave synthesis (lightweight)
- **Variables per context**: max **500** variables (prevents unbounded memory growth)
- **Maximum string length**: max **204,800** characters (~200 KB)
- **Maximum loop iterations per execution**: max **100,000** iterations per `Execute` call
- **Maximum execution time per block**: hard time budget of **1,000 ms** per `Execute` call

Additionally:

- `Canvas.setSize` has a **cooldown per terminal** (10 seconds) to prevent resize spam.
- Each `print("#UI{ ... }")` is executed in its own VM run; previous run can be stopped cleanly.

If a script exceeds a limit, execution is aborted and an error is logged to BepInEx.

---

## Building From Source

Project layout (high level):

- `src` — plugin source and project files
- `tests` — automated tests for the VM and scripting engine

Build commands (from the `src` directory):

```bash
# BepInEx 5
dotnet build -c Debug-BepInEx5
dotnet build -c Release-BepInEx5

# BepInEx 6
dotnet build -c Debug-BepInEx6
dotnet build -c Release-BepInEx6
```

Output DLLs land under:

- `src/bin/[Configuration]/BepInEx5/netstandard2.1/`
- `src/bin/[Configuration]/BepInEx6/netstandard2.1/`

You can run tests from the `tests` folder:

```bash
dotnet test
```

---

## License

This project is licensed under the MIT License. See `LICENSE` for details.