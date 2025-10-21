// WebGPU initialization and render loop bootstrap with FPS + resize handling

const initWebGPU = async () => {
  if (!('gpu' in navigator)) {
    console.error('WebGPU not supported in this browser.');
    return false;
  }
  try {
    const adapter = await navigator.gpu.requestAdapter();
    if (!adapter) {
      console.error('Failed to acquire GPU adapter.');
      return false;
    }
    const device = await adapter.requestDevice();
    Module.preinitializedWebGPUDevice = device;
    return true;
  } catch (e) {
    console.error('WebGPU init error:', e);
    return false;
  }
};

// Create a lightweight FPS overlay
const createFpsOverlay = () => {
  const el = document.createElement('div');
  el.id = 'fps-overlay';
  el.style.position = 'fixed';
  el.style.right = '8px';
  el.style.top = '8px';
  el.style.padding = '6px 8px';
  el.style.background = 'rgba(0,0,0,0.5)';
  el.style.color = '#0f0';
  el.style.fontFamily = 'monospace';
  el.style.zIndex = 10000;
  el.textContent = 'FPS';
  document.body.appendChild(el);
  return el;
};

const startLoop = async () => {
  let lastTime = performance.now();
  let frames = 0;
  let fpsEl = createFpsOverlay();
  let lastFpsUpdate = performance.now();

  const  frame = async (t) => {
    frames++;
    // Update FPS every 500ms
    if (t - lastFpsUpdate > 500) {
      const fps = Math.round((frames * 1000) / (t - lastFpsUpdate));
      fpsEl.textContent = `FPS: ${fps}`;
      frames = 0;
      lastFpsUpdate = t;
    }

    if (typeof DotNet !== 'undefined' && DotNet.invokeMethodAsync) {
      // best-effort; ignore errors
      await DotNet.invokeMethodAsync('Mandelbrot.web', 'Tick').catch(e => { /* swallow */ });
    }

    (frame);
  };
  requestAnimationFrame(frame);
};

// Debounced resize notifier to Blazor
let resizeTimeout = null;
window.addEventListener('resize', () => {
  if (resizeTimeout) clearTimeout(resizeTimeout);
  resizeTimeout = setTimeout(() => {
    if (typeof DotNet !== 'undefined' && DotNet.invokeMethodAsync) {
      DotNet.invokeMethodAsync('Mandelbrot.web', 'OnResize');//.catch(e => { /* swallow */ });
    }
  }, 200);
});

// Initialize and start
initWebGPU().then(async ok => {
  if (ok) {
    await startLoop();
  } else {
    console.warn('Render loop not started due to failed WebGPU init.');
  }
});

// ensure Module.canvas is set to the DOM canvas before native calls
if (typeof Module !== 'undefined' && !Module.canvas) {
  Module.canvas = document.querySelector('canvas') || document.getElementById('canvas');
}