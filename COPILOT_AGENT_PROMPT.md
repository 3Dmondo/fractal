# Task: Replace triangle with Mandelbrot rendering (Blazor WebAssembly / WebGPU)

Objective
- Replace the triangle demo in `Mandelbrot.Web/Pages/Home.razor.cs` with a Mandelbrot set renderer.
- Start with single-precision (float) shader math (WGSL) to ensure WebGL/WebGPU compatibility; test double precision later.
- Add interactive pan & zoom like the desktop `Mandelbrot/Window.cs` (mouse drag, wheel zoom) and support mobile (touch/pinch). 
- Make minimal changes per step; after each step code must be functional and testable.
- Produce readable, maintainable, testable code that follows SOLID principles.

Scope & constraints
- Do not rewrite unrelated parts of the project. Modify only files necessary for the feature.
- Keep the same rendering path (use the existing pipeline creation and draw code in `Home.razor.cs`) where practical to minimize changes.
- Use a single uniform block for view parameters (center, scale, view/aspect) and update it on interaction.
- Use WGSL single-precision fragment shader derived from `Mandelbrot/Shaders/shader.frag` (convert `double` and `dvec2` to `f32`/`vec2<f32>`). Use the vertex shader pattern that maps the existing vertex buffer to clip space and passes coordinates to the fragment shader.

Required knowledge for the agent
- Blazor WebAssembly + JS interop basics
- Emscripten `Module.preinitializedWebGPUDevice` pattern already present in `wwwroot/webgpu.js`
- Evergine.Bindings.WebGPU usage in `Home.razor.cs`
- Converting GLSL fragment code to WGSL (single-precision)
- Pointer/touch event handling in Blazor (or via a small JS helper if needed)

Plan (step-by-step, minimal changes, test at each step)

Step 0 — Preparation
- Inspect the following files and run the app to confirm a baseline:
  - `Mandelbrot.Web/Pages/Home.razor`
  - `Mandelbrot.Web/Pages/Home.razor.cs`
  - `Mandelbrot/wwwroot/webgpu.js`
  - `Mandelbrot/Window.cs` (for interaction logic reference)
  - `Mandelbrot/Shaders/shader.vert` and `Mandelbrot/Shaders/shader.frag`
- Confirm the app boots and the triangle is rendered (or previously failing behavior is now fixed by earlier changes).

Test 0 (manual):
- Launch the app in a browser that supports WebGPU (Chrome Canary/Edge with flag if needed) and verify the triangle demo still works.

Step 1 — Create a WGSL single-precision Mandelbrot fragment shader and matching vertex shader
- Convert the algorithm from `shader.frag` to WGSL:
  - Replace `dvec2` and `double` with `vec2<f32>` and `f32`.
  - Implement an iteration limit uniform (e.g., `uMaxIterations : i32`).
  - Compute color using a single-precision variant of the existing palette logic.
- Provide a simple vertex shader that uses the existing vertex buffer (`aPos` attribute) to produce clip-space output and pass coordinates to the fragment shader.
- In `Home.razor.cs` replace the `triangleWGSL` string with the new pair of WGSL shaders (vertex + fragment) and compile them using the existing `create_shader` helper.

Minimal code changes:
- Replace only the shader text constant(s) in `Home.razor.cs`.

Test 1 (manual):
- Rebuild and run the app. Verify the canvas now shows a Mandelbrot-like image (single-precision).
- If nothing appears, open the browser console and copy errors.

If Step 1 fails: ask questions — e.g. whether to change the vertex buffer layout or to draw a full-screen quad via vertex_index in the vertex shader.

Step 2 — Add view uniforms (center and scale) and hook them up in C# — COMPLETE
- Define a uniform struct in WGSL, e.g.:
  ```wgsl
  struct ViewParams {
    center : vec2<f32>;
    scale : f32; // or 1/zoom
    aspect : f32;
    maxIter : i32;
  };
  @group(0) @binding(0) var<uniform> uView : ViewParams;
  ```
- In `Home.razor.cs` create a uniform buffer (like the existing uniform usage pattern) for `ViewParams` and write initial values that center the view and set scale.
- Ensure the shader uses `uView.center`, `uView.scale` and `uView.aspect` to compute coordinates like the desktop shader.

Minimal changes:
- Add uniform buffer creation and `wgpuQueueWriteBuffer` updates in `Home.razor.cs` near where `ubuffer` was created.

Test 2 (manual):
- Rebuild and run. Confirm the Mandelbrot renders with the initial center/scale values.

Before Step 3 — ensure initial rendering matches canvas size and aspect ratio
- To match desktop `Mandelbrot/Window.cs` behavior, compute the uniform `view` from the canvas aspect ratio and initial `scale`:
  - Aspect = height / width
  - view = (aspect, 1) / scale
- Update `Home.razor.cs` when creating the uniform buffer to compute `viewX`/`viewY` from the measured canvas `width` and `height`, for example:
  ```csharp
  float scale = 1.0f;
  float viewX = (float)height / (float)width / scale; // aspect / scale
  float viewY = 1.0f / scale;
  ```
- Use these values when writing the uniform buffer so the first rendered frame respects canvas aspect ratio and matches the desktop view math.

Test (manual):
- Rebuild and run. The Mandelbrot rendering should not appear stretched; panning and zoom math later will match the desktop `Window.cs` conventions.

Step 3 — Implement panning (mouse/touch drag), zoom (wheel/pinch), and window resize
- Preferred approach: use Blazor pointer events on the canvas in `Home.razor`:
  - Add `@onpointerdown`, `@onpointermove`, `@onpointerup`, and `@onwheel` handlers.
  - In `Home.razor.cs` add corresponding `OnPointerDown`, `OnPointerMove`, `OnPointerUp`, `OnWheel` methods that update `center`/`scale` values.
  - For mobile pinch: implement pointer capture for multiple pointers and calculate distance delta for pinch, or if simpler, add a small JS helper to detect pinch and call .NET method via `DotNet.invokeMethodAsync`.
  - **Handle window/canvas resize:** update view parameters and redraw Mandelbrot when the canvas size changes, matching desktop behavior.
- After updating `center`/`scale`/`view`, write the updated `ViewParams` to the uniform buffer with `wgpuQueueWriteBuffer` and call the existing `draw` (or re-run `Run`'s draw path) to present the updated image.

Minimal changes:
- Add event handlers and uniform buffer update code only.

Test 3 (manual):
- On desktop: click-and-drag the canvas to pan; use the mouse wheel to zoom centered at pointer position. Resize the window and confirm Mandelbrot view updates responsively.
- On mobile: test touch drag to pan and pinch-to-zoom (if implemented). If pinch handled in JS, verify events reach C# and update view.

Step 4 — Improve interaction UX — COMPLETE
- Smooth zoom toward pointer by computing world-space pointer location and adjusting center when zooming (same math as `Window.cs`).
- Add momentum or pinch smoothing only after basic correctness.

Minimal changes:
- Adjust the zoom handlers to modify `center` as in `Window.cs` logic.

Test 4 (manual):
- Verify zoom focuses on the pointer and panning feels correct.

Step 5 — Implement window/canvas resize and mobile/touch interaction
- **Window/canvas resize:**
  - Detect when the browser window or canvas size changes.
  - Recompute aspect ratio and view parameters.
  - Update the uniform buffer and redraw Mandelbrot to fit the new size.
- **Mobile/touch interaction:**
  - Add touch event handlers for pan (single finger drag) using Blazor pointer events.
  - Add pinch-to-zoom (multi-touch) support, preferring Blazor pointer events. Use a JS helper only if Blazor cannot provide required multi-touch data.
  - Ensure pan/zoom logic matches desktop behavior for consistency.

Minimal changes:
- Add event handlers and logic for resize and mobile/touch.
- Update uniform buffer and redraw as needed.

Test 5 (manual):
- Resize the browser window and confirm Mandelbrot view updates responsively.
- On mobile: drag to pan, pinch to zoom. Confirm interaction matches desktop.

Step 6 — Refactor and follow SOLID
- Extract shader strings into small helpers or separate resource files (still kept minimal).
- Encapsulate view state and uniform buffer logic into a small `WebGpuViewState` class (single responsibility) with methods: `UpdateCenter`, `UpdateScale`, `WriteToBuffer`.
- Ensure the `Home` component delegates rendering and state update responsibilities to these classes.
- Keep the public surface of the component minimal for testability.

Test 6 (manual):
- Run and smoke-test all interactions again. Ensure code is readable and unit-testable where possible.

Step 7 — Optional: try double precision
- If the single-precision implementation works, attempt to reproduce the algorithm in double precision:
  - WebGPU/WGSL does NOT have built-in 64-bit floats in all implementations; check target browser support. If WGSL supports `f64` on your platform and the Evergine bindings support it, port the shader back to double precision.
- Alternatively emulate higher precision with techniques such as `vec2<f32>` high/low decomposition.

Test 7 (manual):
- If attempting true double precision, validate results vs single-precision and verify performance.

Safety & debugging guidance
- If `emscripten_webgpu_get_device()` fails, ensure `wwwroot/webgpu.js` is loaded before Blazor (we already added the script to `index.html`).
- Use browser DevTools Console for WebGPU errors and the Blazor console logs.
- Log uniform values and pointer coords as needed for debugging.

Questions to ask the user (do not assume)
- Preferred user interaction API for mobile pinch detection: Blazor pointer events or a small JS helper that invokes C#? (If unsure, default to Blazor pointer events for simplicity.)
- Should the Mandelbrot shader use the existing vertex/index buffers (minimal change) or draw a fullscreen quad using vertex_index (simpler shader but requires changing the draw call)?
- Target browsers/devices for testing (Chromium-based browsers recommended for WebGPU today).

Deliverables for each step
- A small PR or patch that modifies only the necessary files.
- A one-line description of changes and how to test them.
- If a step fails, revert to the last working step and open an issue describing the failure.

Accept these constraints and proceed step-by-step. If anything is unclear at any step, ask before making further changes.