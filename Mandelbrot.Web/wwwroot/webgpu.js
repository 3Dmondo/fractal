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

  // expose function globally so Blazor can call it
  window.initWebGPU = initWebGPU;

  if (document.readyState === 'loading') {
    window.addEventListener('DOMContentLoaded', initWebGPU);
  } else {
    // DOM already ready
    initWebGPU();
  }
})();