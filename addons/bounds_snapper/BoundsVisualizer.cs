using Godot;
using System.Collections.Generic;

public class BoundsVisualizer
{
	private readonly MultiMeshInstance3D _targetIndicators;
	private readonly MultiMesh _multiMesh;
	private readonly MeshInstance3D _sourceIndicator;
	private readonly MeshInstance3D _boundsWireframe;
	private readonly ImmediateMesh _wireframeMesh;

	public BoundsVisualizer()
	{
		var dotGradient = new Gradient
		{
			Offsets = [0.0f, 0.65f, 0.75f, 0.95f, 1.0f],
			Colors = [Colors.White, Colors.White, Colors.Black, Colors.Black, Colors.Transparent]
		};

		var dotTexture = new GradientTexture2D
		{
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1.0f, 0.5f),
			Width = 64,
			Height = 64,
			Gradient = dotGradient
		};

		var quadMesh = new QuadMesh { Size = new Vector2(0.15f, 0.15f) };
		
		_multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			Mesh = quadMesh
		};

		_targetIndicators = new MultiMeshInstance3D
		{
			Multimesh = _multiMesh,
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				VertexColorUseAsAlbedo = true,
				AlbedoTexture = dotTexture,
				BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
				NoDepthTest = true
			},
			TopLevel = true,
			Visible = false
		};
		
		_sourceIndicator = new MeshInstance3D 
		{ 
			Mesh = quadMesh, 
			MaterialOverride = new StandardMaterial3D 
			{ 
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, 
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = Colors.Red, 
				AlbedoTexture = dotTexture,
				BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
				NoDepthTest = true 
			}, 
			TopLevel = true, 
			Visible = false 
		};

		_wireframeMesh = new ImmediateMesh();
		
		_boundsWireframe = new MeshInstance3D
		{
			Mesh = _wireframeMesh,
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				VertexColorUseAsAlbedo = true,
				NoDepthTest = true
			},
			TopLevel = true,
			Visible = false
		};
	}

	public Vector3 SourceIndicatorPosition
	{
		get => _sourceIndicator.GlobalPosition;
		set => _sourceIndicator.GlobalPosition = value;
	}

	public void SetSourceIndicatorVisible(bool visible)
	{
		_sourceIndicator.Visible = visible;
	}

	public void UpdateParents(Node rootNode)
	{
		UpdateParent(_targetIndicators, rootNode);
		UpdateParent(_sourceIndicator, rootNode);
		UpdateParent(_boundsWireframe, rootNode);
	}

	private void UpdateParent(Node3D indicator, Node rootNode)
	{
		if (indicator.GetParent() != rootNode)
		{
			indicator.GetParent()?.RemoveChild(indicator);
			rootNode.AddChild(indicator);
		}
	}

	public bool IsVisualizerNode(Node node)
	{
		return node == _sourceIndicator || node == _targetIndicators || node == _boundsWireframe;
	}

	public void HideTargetAndWireframe()
	{
		_targetIndicators.Visible = false;
		_boundsWireframe.Visible = false;
	}

	public void HideAll()
	{
		HideTargetAndWireframe();
		_sourceIndicator.Visible = false;
	}

	public void Dispose()
	{
		_targetIndicators?.QueueFree();
		_sourceIndicator?.QueueFree();
		_boundsWireframe?.QueueFree();
	}

	public void UpdateVisualizers(Camera3D camera, Vector2 mousePos, Vector3? activeTargetCorner, List<Vector3> cornersToDraw, List<(Aabb bounds, Transform3D xform)> boundsToDraw)
	{
		List<(Vector3 pos, float alpha)> visibleCorners = [];
		float maxVisualDist = 200f; 

		foreach (Vector3 globalCorner in cornersToDraw)
		{
			if (camera.IsPositionBehind(globalCorner)) continue;

			Vector2 screenPos = camera.UnprojectPosition(globalCorner);
			float dist = screenPos.DistanceTo(mousePos);

			if (dist < maxVisualDist)
			{
				float alpha = 1.0f - (dist / maxVisualDist);
				
				if (activeTargetCorner.HasValue && activeTargetCorner.Value.IsEqualApprox(globalCorner))
				{
					alpha = 1.0f;
				}

				visibleCorners.Add((globalCorner, alpha));
			}
		}

		_multiMesh.InstanceCount = visibleCorners.Count;
		for (int i = 0; i < visibleCorners.Count; i++)
		{
			Transform3D xform = new Transform3D(Basis.Identity, visibleCorners[i].pos);
			_multiMesh.SetInstanceTransform(i, xform);
			_multiMesh.SetInstanceColor(i, new Color(1, 1, 0, visibleCorners[i].alpha)); 
		}
		
		_targetIndicators.Visible = visibleCorners.Count > 0;

		_wireframeMesh.ClearSurfaces();
		bool surfaceBegun = false;
		int[] edges = [0, 1, 1, 3, 3, 2, 2, 0, 4, 5, 5, 7, 7, 6, 6, 4, 0, 4, 1, 5, 2, 6, 3, 7];

		foreach (var item in boundsToDraw)
		{
			Vector3[] pts = new Vector3[8];
			float[] alphas = new float[8];
			bool boxVisible = false;

			for (int i = 0; i < 8; i++)
			{
				pts[i] = item.xform * item.bounds.GetEndpoint(i);
				if (camera.IsPositionBehind(pts[i]))
				{
					alphas[i] = 0f;
					continue;
				}

				Vector2 screenPos = camera.UnprojectPosition(pts[i]);
				float dist = screenPos.DistanceTo(mousePos);

				if (dist < maxVisualDist)
				{
					alphas[i] = 1.0f - (dist / maxVisualDist);
					boxVisible = true;
				}
				else
				{
					alphas[i] = 0f;
				}
			}

			if (boxVisible)
			{
				if (!surfaceBegun)
				{
					_wireframeMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
					surfaceBegun = true;
				}
				for (int i = 0; i < edges.Length; i++)
				{
					int idx = edges[i];
					_wireframeMesh.SurfaceSetColor(new Color(1, 1, 0, alphas[idx] * 0.2f));
					_wireframeMesh.SurfaceAddVertex(pts[idx]);
				}
			}
		}

		if (surfaceBegun)
		{
			_wireframeMesh.SurfaceEnd();
			_boundsWireframe.Visible = true;
		}
		else
		{
			_boundsWireframe.Visible = false;
		}
	}
}