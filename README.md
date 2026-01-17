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

## Security & Limits

To keep things safe and responsive, the VM enforces several limits:

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