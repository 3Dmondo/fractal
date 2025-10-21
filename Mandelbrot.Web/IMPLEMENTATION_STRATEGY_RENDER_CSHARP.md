Goal

- Implement a simple, client-side Mandelbrot renderer that runs inside the `Mandelbrot.Web` Blazor WebAssembly project and performs rendering from C# when the view changes (no continuous loop).

Constraints & decisions (updated)

- Use the existing `Evergine.Bindings.WebGPU` library already referenced by the project. There is no need to implement low-level WebGL or Emscripten bindings.
- Reuse the desktop `Mandelbrot\Window.cs` and shader sources under `Mandelbrot\Shaders` as the algorithmic reference and to derive the WGSL implementation.
- Follow the HelloTriangleWasm page wiring pattern (`HelloTriangleWasm\Pages\Home.razor(.cs)`) for bootstrapping and calling into C# from JS. Keep the runtime interactions minimal: JS forwards pointer/wheel/resize events to C# static invokable methods and C# performs the rendering when state changes.
- `Mandelbrot.Web\Helpers.cs` is available and can be used to convert managed strings into native pointers when creating WGSL shader modules via Evergine helper patterns.
- Rendering happens only when the scene changes (resize, pan, zoom). No animation loop is required.

High-level approach (preferred, using Evergine.Bindings.WebGPU)

1. Use Evergine.Bindings.WebGPU as the native binding surface
   - Reuse the same pattern used in `HelloTriangleWasm\Pages\Home.razor.cs` (the `Run()` example) as a direct template: call `emscripten_webgpu_get_device()`, create swapchain from an HTML canvas selector, create shader modules, pipeline, buffers, bind groups, and issue draw calls from C#.
   - Keep code simple and local: a `MandelbrotRenderer` class will own device resources and expose simple methods to initialize, update uniforms, resize the swap chain, and draw.

2. Port shaders to WGSL
   - The desktop shaders are the reference. Port the fragment shader algorithm from `Mandelbrot\Shaders\shader.frag` to WGSL (single-precision floats) and produce a minimal vertex shader that draws a fullscreen quad (the `gl_VertexID` trick used on desktop maps well to WGSL vertex shader with no vertex buffers required).
   - Expose uniforms: `uCenter : vec2<f32>`, `uView : vec2<f32>` (or `uScale` + `uAspect`), and `uMaxIter : u32`.

3. Implement `MandelbrotRenderer` in C# (simple, Evergine/WebGPU style)
   - Add `Mandelbrot.Web/Rendering/MandelbrotRenderer.cs`.
   - Responsibilities:
     - `Initialize(string canvasSelector)` — create surface/swapchain from the canvas selector using the exact Evergine/emscripten pattern shown in `HelloTriangleWasm`.
     - Create WGSL shader modules from string literals (use `Helpers.ToPointer` if needed), create pipeline layout, render pipeline, vertex state (fullscreen), and uniform buffer(s).
     - `Resize(double cssWidth, double cssHeight)` — recreate swapchain or update internal width/height and viewport uniforms.
     - `UpdateView(Vector2d center, double scale, int maxIterations)` — write uniforms into the uniform buffer (map or use queue write helper) and mark state if needed.
     - `Draw()` — acquire current swap chain view, encode render pass, set pipeline and bind groups, draw the fullscreen triangle/quad, submit, release temporary objects.
   - Keep the implementation minimal and model it after the `Run()` routine from the HelloTriangle example: the same Evergine functions can be used to create shader modules, pipelines, buffers and record/submit commands.

4. Page wiring in Blazor
   - Update `Mandelbrot.Web/Pages/Home.razor` and `Home.razor.cs` following the `HelloTriangleWasm` template:
     - Add `<canvas id="mandelbrot-canvas"></canvas>` and include a small `wwwroot/mandelbrot.js` script.
     - In `Home.razor.cs` maintain view state (`Center : Vector2d`, `Scale : double`, `AspectRatio` derived), an instance of `MandelbrotRenderer`, and a dirty flag.
     - Implement `[JSInvokable]` static methods: `Tick()` (call Draw when dirty), `OnResize()` (query canvas CSS size and call renderer.Resize), `OnWheel(float deltaY, float clientX, float clientY)`, `OnPointerDown(float x, float y)`, `OnPointerMove(float x, float y)`, `OnPointerUp()` — use the same mouse-to-world mapping math from `Mandelbrot\Window.cs` to compute `Center` and update `Scale`.
     - Instantiate and initialize `MandelbrotRenderer` on first use (e.g., OnAfterRenderAsync or first Tick), using the canvas selector `"mandelbrot-canvas"`.

5. JavaScript helpers (small)
   - Add `wwwroot/mandelbrot.js` that attaches pointer and wheel listeners to the canvas and forwards events to C# via `DotNet.invokeMethodAsync('{ASSEMBLY_NAME}', 'OnWheel', deltaY, x, y)` etc. Also call `DotNet.invokeMethodAsync('{ASSEMBLY_NAME}', 'OnResize')` on initial load and debounced window resize.
   - Unlike the previous WebGPU-centric plan, the JS file only forwards events; all heavy work is done inside C# using Evergine bindings.

6. Input & coordinate math (reuse desktop mapping)
   - Use the same formulas from `Mandelbrot\Window.cs`:
     - `AspectRatio = canvasHeight / canvasWidth` (note Y/X to match earlier code) and `View = new Vector2d(AspectRatio, 1.0) / Scale`.
     - Convert canvas pixel coordinates to NDC and then to complex-plane using `Center + (pixel / canvasSize * 2 - One) / View * InvertMouseX`.
     - For wheel zoom: compute world coords under cursor before change, update `Scale`, recompute world coords and adjust `Center` so the cursor remains anchored.
     - For drag/pan: convert pixel delta to complex-plane delta and subtract from `Center`.

7. Render-on-change lifecycle
   - When input handlers mutate state, set a dirty flag and call `Draw()` (or call `Tick()` which will call Draw if dirty). The draw call will update uniform buffer(s) and issue a single frame of rendering; then clear dirty state.

Concrete files to add / update (minimal)

- New: `Mandelbrot.Web/Rendering/MandelbrotRenderer.cs`
  - Implement Evergine/WebGPU resource creation, uniform updates and `Draw()` using the `HelloTriangleWasm` example as a template. Keep code concise and focused on the Mandelbrot uniform inputs and a fullscreen render.

- Update: `Mandelbrot.Web/Pages/Home.razor` and `Home.razor.cs`
  - Add canvas with id `mandelbrot-canvas` and include `mandelbrot.js`.
  - In `Home.razor.cs` extend the existing skeleton (it already contains `Tick()` and `OnResize()` static methods) to hold renderer and the view math from `Mandelbrot\Window.cs`.

- New: `Mandelbrot.Web/wwwroot/mandelbrot.js`
  - Forward `pointerdown`, `pointermove`, `pointerup`, `wheel`, and debounced `resize` events to the static JSInvocable methods on the Blazor page.

- (Optional) New: `Mandelbrot.Web/wwwroot/shaders/mandelbrot.wgsl` or embed WGSL shader source as C# string constants in `MandelbrotRenderer.cs`.

Step-by-step implementation plan

1. Port the fragment logic from `Mandelbrot\Shaders\shader.frag` into WGSL (floats). Create a minimal WGSL vertex shader (fullscreen triangle) and test by compiling into a shader module in `MandelbrotRenderer`.
2. Implement `MandelbrotRenderer.Initialize(canvasSelector)` using the `HelloTriangleWasm` `Run()` code as reference. Create pipeline, uniform buffer and any needed bind group layout. Use `Helpers.ToPointer` for string->pointer when creating shader modules via the Evergine helper pattern.
3. Implement `UpdateView(center, scale)` to write uniforms to the uniform buffer (use `wgpuQueueWriteBuffer` or equivalent existing helper patterns). Implement `Resize` to recreate swapchain or update cached dimensions.
4. Wire up `Home.razor(.cs)` to instantiate the renderer, maintain view state and call `Draw()` when dirty. Implement the JSInvokable handlers for input and use the same coordinate math as `Mandelbrot\Window.cs`.
5. Add `wwwroot/mandelbrot.js` to forward events and call `OnResize` on load.
6. Test in a WebGPU-capable browser (or provide a friendly message / fallback if WebGPU is unavailable).

Notes & trade-offs

- Using the Evergine WebGPU bindings closely follows the existing HelloTriangleWasm example and keeps rendering logic in C# while avoiding manual native bindings.
- WGSL and single-precision floats mean extreme zooms will differ from desktop double-precision rendering; document this limitation.
- Keep the renderer intentionally simple: fullscreen render with uniforms, render on change only.

Acceptance criteria

- The `Mandelbrot.Web` page renders the Mandelbrot set inside `<canvas id="mandelbrot-canvas">`.
- Pan, zoom (mouse wheel), and resize are implemented and update the view correctly; rendering occurs only when parameters change.
- Implementation reuses Evergine.Bindings.WebGPU patterns from `HelloTriangleWasm` and desktop mapping from `Mandelbrot\Window.cs`.

Next immediate actions

1. I will update `Mandelbrot.Web/Pages/Home.razor(.cs)` to follow `HelloTriangleWasm` template and add canvas and script include.
2. Add `MandelbrotRenderer.cs` in `Mandelbrot.Web/Rendering/` that uses Evergine.Bindings.WebGPU APIs to create shader modules, pipeline and draw.
3. Add `wwwroot/mandelbrot.js` to forward pointer/wheel/resize events to Blazor static methods.
4. Port the fragment shader to WGSL and include it either as an embedded string or as a `wwwroot` asset compiled at runtime.

