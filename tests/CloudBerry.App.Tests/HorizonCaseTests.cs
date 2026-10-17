using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using CloudBerry.App.Controls;
using CloudBerry.App.ViewModels;

namespace CloudBerry.App.Tests;

public sealed class HorizonCaseTests
{
    [Fact]
    public void FocusPreservesGeometryAndCoverMaterials()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var control = new HorizonGameCase3D { Profile = GameCaseProfile.Resolve("Nintendo Switch") };
                var viewport = Assert.IsType<Viewport3D>(control.Children[0]);
                var visual = Assert.IsType<ModelVisual3D>(viewport.Children[0]);
                var camera = Assert.IsType<PerspectiveCamera>(viewport.Camera);
                Assert.Equal(0, (camera.Position + camera.LookDirection).X, 6);
                Assert.Equal(0, (camera.Position + camera.LookDirection).Y, 6);
                Assert.Single(control.Children.Cast<System.Windows.UIElement>());
                var model = Assert.IsType<Model3DGroup>(visual.Content);
                var front = Assert.IsType<GeometryModel3D>(model.Children[3]);
                var mesh = Assert.IsType<MeshGeometry3D>(front.Geometry);
                Assert.Equal(control.Profile.Width / control.Profile.Height, mesh.Bounds.SizeX / mesh.Bounds.SizeY, 6);
                control.IsCaseFocused = true;
                Assert.Same(visual, viewport.Children[0]);
                Assert.Same(front, model.Children[3]);
                control.IsCaseFocused = false;
                Assert.Same(visual, viewport.Children[0]);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Case construction timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
