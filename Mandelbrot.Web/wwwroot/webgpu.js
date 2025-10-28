(function () {
  if (window.__wgpuInitDone) return;
  window.__wgpuInitDone = true;

  // Try to set Module.canvas synchronously if DOM element already exists
  if (typeof Module !== 'undefined' && !Module.canvas) {
    Module.canvas = document.getElementById('canvas') || document.querySelector('canvas');
  }

  const initWebGPU = async () => {
    try {
      // ensure Module.canvas is set (fallback)
      if (typeof Module !== 'undefined' && !Module.canvas) {
        Module.canvas = document.getElementById('canvas') || document.querySelector('canvas');
      }
      if (!('gpu' in navigator)) {
        console.warn('WebGPU not supported in this browser.');
        return;
      }
      const adapter = await navigator.gpu.requestAdapter();
      if (!adapter) {
        console.warn('Failed to acquire GPU adapter.');
        return;
      }
      const device = await adapter.requestDevice();
      if (typeof Module !== 'undefined') Module.preinitializedWebGPUDevice = device;
    } catch (e) {
      console.error('initWebGPU error', e);
      throw e;
    }
  };

  // Pinch-to-zoom handler for mobile/touch
  function setupPinchHandler() {
    const canvas = document.getElementById('canvas') || document.querySelector('canvas');
    if (!canvas) return; // Only set up if canvas exists
    let lastDist = null;
    function getTouchInfo(evt) {
      if (evt.touches.length !== 2) return null;
      const t1 = evt.touches[0], t2 = evt.touches[1];
      const dx = t1.clientX - t2.clientX;
      const dy = t1.clientY - t2.clientY;
      const dist = Math.sqrt(dx * dx + dy * dy);
      const centerX = (t1.clientX + t2.clientX) / 2;
      const centerY = (t1.clientY + t2.clientY) / 2;
      return { dist, centerX, centerY };
    }
    canvas.addEventListener('touchstart', function (evt) {
      if (evt.touches.length === 2) {
        const info = getTouchInfo(evt);
        lastDist = info.dist;
      }
    }, { passive: false });
    canvas.addEventListener('touchmove', function (evt) {
      if (evt.touches.length === 2) {
        evt.preventDefault();
        const info = getTouchInfo(evt);
        if (lastDist != null && info) {
          const scaleDelta = -info.dist / lastDist;
          window.DotNet && window.DotNet.invokeMethodAsync && window.DotNet.invokeMethodAsync(
            'Mandelbrot.Web', 'OnPinch', info.centerX, info.centerY, scaleDelta
          );
        }
        lastDist = info.dist;
      }
    }, { passive: false });
    canvas.addEventListener('touchend', function (evt) {
      if (evt.touches.length < 2) {
        lastDist = null;
      }
    });
  }

  // expose function globally so Blazor can call it
  window.initWebGPU = initWebGPU;
  window.setupPinchHandler = setupPinchHandler;

  // Remove automatic setupPinchHandler call
  if (document.readyState === 'loading') {
    window.addEventListener('DOMContentLoaded', function() {
      initWebGPU();
      // setupPinchHandler(); // Removed
    });
  } else {
    // DOM already ready
    initWebGPU();
    // setupPinchHandler(); // Removed
  }
})();