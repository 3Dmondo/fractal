using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;

namespace Mandelbrot.Web.Pages;

public partial class Home
{

  [Inject] private IJSRuntime JS { get; set; } = null!;


  [JSInvokable]
  public static Task Tick() => Instance?.AdvanceAndRenderFrame() ?? Task.CompletedTask;

  [JSInvokable]
  public static Task OnResize() => Instance?.HandleResize() ?? Task.CompletedTask;

  private static Home? Instance;
  public Home() { Instance = this; }

  private Task AdvanceAndRenderFrame()
  {
    return Task.CompletedTask;
  }


  private Task HandleResize()
  {
    return Task.CompletedTask;
  }
}
