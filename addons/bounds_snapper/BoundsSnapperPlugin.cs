using Godot;

[Tool]
public partial class BoundsSnapperPlugin : EditorPlugin
{
	private Button _snapToggleBtn;
	private BoundsVisualizer _visualizer;
	private BoundsSnapController _controller;

	public override void _EnterTree()
	{
		_snapToggleBtn = new Button
		{
			Text = "●",
			TooltipText = "Bounds Snap\nToggle ON, then hold B + LMB & Drag to snap bounds.",
			FocusMode = Control.FocusModeEnum.None,
			ToggleMode = true,
			Flat = true
		};
		
		Color accentColor = EditorInterface.Singleton.GetEditorSettings().GetSetting("interface/theme/accent_color").AsColor();
		_snapToggleBtn.AddThemeColorOverride("font_pressed_color", accentColor);
		_snapToggleBtn.AddThemeColorOverride("font_hover_pressed_color", accentColor);
		
		InjectButtonIntoTransformToolbar();

		_visualizer = new BoundsVisualizer();
		_controller = new BoundsSnapController(this, _visualizer);
	}

	public override void _ExitTree()
	{
		if (_snapToggleBtn != null && GodotObject.IsInstanceValid(_snapToggleBtn))
		{
			_snapToggleBtn.GetParent()?.RemoveChild(_snapToggleBtn);
			_snapToggleBtn.QueueFree();
		}
		
		_visualizer?.Dispose();
	}

	public override bool _Handles(GodotObject @object)
	{
		return @object is Node3D;
	}

	public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
	{
		if (_snapToggleBtn == null || !GodotObject.IsInstanceValid(_snapToggleBtn) || _controller == null)
		{
			return (int)EditorPlugin.AfterGuiInput.Pass;
		}

		if (!_snapToggleBtn.ButtonPressed)
		{
			_controller.StopDragging(false);
			return (int)EditorPlugin.AfterGuiInput.Pass;
		}

		var selectedNodes = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
		if (selectedNodes == null || selectedNodes.Count == 0)
		{
			_controller.StopDragging(false);
			return (int)EditorPlugin.AfterGuiInput.Pass;
		}

		return _controller.ProcessInput(camera, @event, selectedNodes);
	}

	private void InjectButtonIntoTransformToolbar()
	{
		Control dummy = new Control();
		AddControlToContainer(CustomControlContainer.SpatialEditorMenu, dummy);
		
		Node rightMenuContainer = dummy.GetParent();
		Node topToolbar = rightMenuContainer?.GetParent();

		bool injected = false;

		if (topToolbar != null)
		{
			foreach (Node child in topToolbar.GetChildren())
			{
				if (child is HBoxContainer leftBox && leftBox != rightMenuContainer)
				{
					leftBox.AddChild(_snapToggleBtn);
					injected = true;
					break;
				}
			}
		}

		if (rightMenuContainer != null)
		{
			rightMenuContainer.RemoveChild(dummy);
		}
		dummy.QueueFree();

		if (!injected)
		{
			AddControlToContainer(CustomControlContainer.SpatialEditorMenu, _snapToggleBtn);
		}
	}
}