using Godot;
using System.Collections.Generic;

public class BoundsSnapController(EditorPlugin plugin, BoundsVisualizer visualizer)
{
	private bool _isDragging = false;
	private Plane _dragPlane;
	private Vector2 _lastMousePos;
	private bool _wasSnapKeyHeld = false;
	private readonly Dictionary<Node3D, Vector3> _nodeOffsets = [];
	private readonly Dictionary<Node3D, Transform3D> _initialTransforms = [];
	private List<Vector3> _cachedTargetCorners = [];
	private List<Vector3> _cachedSourceCorners = [];
	private List<(Aabb bounds, Transform3D xform)> _cachedTargetBounds = [];
	private readonly List<(Aabb bounds, Transform3D xform)> _cachedSourceBounds = [];
	private readonly HashSet<Node> _excludedNodes = [];

	public int ProcessInput(Camera3D camera, InputEvent @event, Godot.Collections.Array<Node> selectedNodes)
	{
		bool isSnapKeyHeld = Input.IsPhysicalKeyPressed(Key.B);

		if (@event is InputEventMouse mouseEvent)
		{
			_lastMousePos = mouseEvent.Position;
		}

		if (@event is InputEventMouseButton mouseBtn)
		{
			if (mouseBtn.ButtonIndex == MouseButton.Left)
			{
				if (mouseBtn.Pressed)
				{
					if (isSnapKeyHeld && !_isDragging)
					{
						if (StartSnapDrag(camera, mouseBtn.Position, selectedNodes))
						{
							_isDragging = true;
							_wasSnapKeyHeld = false; 
							return (int)EditorPlugin.AfterGuiInput.Stop;
						}
						else
						{
							GD.Print("[BoundsSnapper] Missed! Click closer to a bounding box corner.");
						}
					}
				}
				else
				{
					if (_isDragging)
					{
						StopDragging(true); 
						return (int)EditorPlugin.AfterGuiInput.Stop;
					}
				}
			}
			else if (mouseBtn.ButtonIndex == MouseButton.Right && mouseBtn.Pressed && _isDragging)
			{
				CancelDrag();
				return (int)EditorPlugin.AfterGuiInput.Stop;
			}
		}

		if (_isDragging)
		{
			if (@event is InputEventMouseMotion mouseMotion)
			{
				PerformSnapDrag(camera, mouseMotion.Position);
				return (int)EditorPlugin.AfterGuiInput.Stop;
			}
		}
		else 
		{
			if (isSnapKeyHeld)
			{
				CacheSourceCorners(selectedNodes);
				
				Node rootNode = EditorInterface.Singleton.GetEditedSceneRoot();
				if (rootNode != null)
				{
					visualizer.UpdateParents(rootNode);
				}
				
				visualizer.UpdateVisualizers(camera, _lastMousePos, null, _cachedSourceCorners, _cachedSourceBounds);
				_wasSnapKeyHeld = true;
			}
			else if (_wasSnapKeyHeld)
			{
				visualizer.HideTargetAndWireframe();
				_wasSnapKeyHeld = false;
			}
		}

		return (int)EditorPlugin.AfterGuiInput.Pass;
	}

	public void StopDragging(bool commitUndo)
	{
		if (_isDragging && commitUndo && _initialTransforms.Count > 0)
		{
			EditorUndoRedoManager undoRedo = plugin.GetUndoRedo();
			undoRedo.CreateAction("Bounds Snap");
			
			foreach (var kvp in _initialTransforms)
			{
				if (kvp.Key != null && GodotObject.IsInstanceValid(kvp.Key))
				{
					undoRedo.AddDoProperty(kvp.Key, "global_transform", kvp.Key.GlobalTransform);
					undoRedo.AddUndoProperty(kvp.Key, "global_transform", kvp.Value);
				}
			}
			
			undoRedo.CommitAction(false); 
		}

		_isDragging = false;
		_cachedTargetCorners.Clear();
		_cachedTargetBounds.Clear();
		_nodeOffsets.Clear();
		_initialTransforms.Clear();
		_wasSnapKeyHeld = false;
		
		visualizer.HideAll();
	}

	private void CancelDrag()
	{
		foreach (var kvp in _initialTransforms)
		{
			if (kvp.Key != null && GodotObject.IsInstanceValid(kvp.Key))
			{
				kvp.Key.GlobalTransform = kvp.Value;
			}
		}
		StopDragging(false);
	}

	private void CacheSourceCorners(Godot.Collections.Array<Node> selectedNodes)
	{
		_cachedSourceCorners.Clear();
		_cachedSourceBounds.Clear();

		foreach (Node n in selectedNodes)
		{
			BoundsExtractor.GatherBounds(n, false, _excludedNodes, _cachedSourceBounds, visualizer);
		}
		_cachedSourceCorners = BoundsExtractor.ExtractAndDeduplicateCorners(_cachedSourceBounds);
	}

	private bool StartSnapDrag(Camera3D camera, Vector2 mousePos, Godot.Collections.Array<Node> selectedNodes)
	{
		_excludedNodes.Clear();
		_initialTransforms.Clear();
		
		CacheSourceCorners(selectedNodes);

		foreach (Node n in selectedNodes)
		{
			_excludedNodes.Add(n);
			if (n is Node3D n3d)
			{
				_initialTransforms[n3d] = n3d.GlobalTransform;
			}
		}
		
		Vector3? closestSourceCorner = GetClosestCorner(camera, mousePos, _cachedSourceCorners, 60f, out _); 
		
		if (closestSourceCorner.HasValue)
		{
			_nodeOffsets.Clear();
			foreach (Node n in selectedNodes)
			{
				if (n is Node3D n3d)
				{
					_nodeOffsets[n3d] = n3d.GlobalPosition - closestSourceCorner.Value;
				}
			}

			_dragPlane = new Plane(camera.GlobalTransform.Basis.Z, closestSourceCorner.Value);
			
			Node rootNode = EditorInterface.Singleton.GetEditedSceneRoot();
			if (rootNode != null)
			{
				visualizer.UpdateParents(rootNode);
				visualizer.SourceIndicatorPosition = closestSourceCorner.Value;
				visualizer.SetSourceIndicatorVisible(true);
				
				_cachedTargetCorners.Clear();
				_cachedTargetBounds.Clear();
				
				BoundsExtractor.GatherBounds(rootNode, true, _excludedNodes, _cachedTargetBounds, visualizer);
				_cachedTargetCorners = BoundsExtractor.ExtractAndDeduplicateCorners(_cachedTargetBounds);
			}

			return true;
		}

		return false;
	}

	private void PerformSnapDrag(Camera3D camera, Vector2 mousePos)
	{
		Vector3? closestTargetCorner = GetClosestCorner(camera, mousePos, _cachedTargetCorners, 50f, out _);
		Vector3 newBasePosition;

		if (closestTargetCorner.HasValue)
		{
			newBasePosition = closestTargetCorner.Value;
			visualizer.SourceIndicatorPosition = newBasePosition;
		}
		else
		{
			Vector3 rayOrigin = camera.ProjectRayOrigin(mousePos);
			Vector3 rayDir = camera.ProjectRayNormal(mousePos);
			
			float nd = _dragPlane.Normal.Dot(rayDir);
			if (Mathf.Abs(nd) > 0.00001f)
			{
				float t = (_dragPlane.D - _dragPlane.Normal.Dot(rayOrigin)) / nd;
				newBasePosition = rayOrigin + rayDir * t;
				visualizer.SourceIndicatorPosition = newBasePosition;
			}
			else
			{
				newBasePosition = visualizer.SourceIndicatorPosition;
			}
		}

		foreach (var kvp in _nodeOffsets)
		{
			if (kvp.Key != null && GodotObject.IsInstanceValid(kvp.Key))
			{
				kvp.Key.GlobalPosition = newBasePosition + kvp.Value;
			}
		}

		visualizer.UpdateVisualizers(camera, mousePos, closestTargetCorner, _cachedTargetCorners, _cachedTargetBounds);
	}

	private Vector3? GetClosestCorner(Camera3D camera, Vector2 mousePos, List<Vector3> corners, float maxDist, out float bestDistance)
	{
		Vector3? bestCorner = null;
		bestDistance = float.MaxValue;

		foreach (Vector3 globalCorner in corners)
		{
			if (camera.IsPositionBehind(globalCorner)) continue;

			Vector2 screenPos = camera.UnprojectPosition(globalCorner);
			float dist = screenPos.DistanceTo(mousePos);

			if (dist < bestDistance && dist < maxDist) 
			{
				bestDistance = dist;
				bestCorner = globalCorner;
			}
		}
		return bestCorner;
	}
}