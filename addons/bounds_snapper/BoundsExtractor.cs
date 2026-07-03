using Godot;
using System.Collections.Generic;

public static class BoundsExtractor
{
	public static List<Vector3> ExtractAndDeduplicateCorners(List<(Aabb bounds, Transform3D xform)> rawBounds)
	{
		List<Vector3> uniqueCorners = [];
		
		foreach (var item in rawBounds)
		{
			for (int i = 0; i < 8; i++)
			{
				Vector3 globalCorner = item.xform * item.bounds.GetEndpoint(i);
				bool isDuplicate = false;
				
				foreach (Vector3 existing in uniqueCorners)
				{
					if (existing.DistanceSquaredTo(globalCorner) < 0.0001f)
					{
						isDuplicate = true;
						break;
					}
				}
				
				if (!isDuplicate)
				{
					uniqueCorners.Add(globalCorner);
				}
			}
		}
		return uniqueCorners;
	}

	public static void GatherBounds(Node node, bool isTarget, HashSet<Node> excludedNodes, List<(Aabb bounds, Transform3D xform)> result, BoundsVisualizer visualizer)
	{
		if (visualizer.IsVisualizerNode(node)) return;
		if (isTarget && excludedNodes.Contains(node)) return;
		if (node is Node3D n3d && !n3d.Visible) return;
		
		if (node is GeometryInstance3D gi)
		{
			result.Add((gi.GetAabb(), gi.GlobalTransform));
		}
		else if (node is CollisionShape3D cs && cs.Shape != null && !cs.Disabled)
		{
			if (GetShapeAabb(cs.Shape) is { } aabb)
			{
				result.Add((aabb, cs.GlobalTransform));
			}
		}
		
		foreach (Node child in node.GetChildren())
		{
			GatherBounds(child, isTarget, excludedNodes, result, visualizer);
		}
	}

	private static Aabb? GetShapeAabb(Shape3D shape)
	{
		if (shape is BoxShape3D box)
			return new Aabb(-box.Size / 2f, box.Size);
		if (shape is SphereShape3D sphere)
			return new Aabb(new Vector3(-sphere.Radius, -sphere.Radius, -sphere.Radius), new Vector3(sphere.Radius * 2, sphere.Radius * 2, sphere.Radius * 2));
		if (shape is CylinderShape3D cyl)
			return new Aabb(new Vector3(-cyl.Radius, -cyl.Height / 2f, -cyl.Radius), new Vector3(cyl.Radius * 2, cyl.Height, cyl.Radius * 2));
		if (shape is CapsuleShape3D cap)
			return new Aabb(new Vector3(-cap.Radius, -cap.Height / 2f, -cap.Radius), new Vector3(cap.Radius * 2, cap.Height, cap.Radius * 2));
		return null;
	}
}