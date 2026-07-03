<div align="center">

# `Bounds Snapper 📌`

[![Godot v4.2+](https://img.shields.io/badge/Godot-4.2%2B-%23478cbf?logo=godot-engine&logoColor=white)](https://godotengine.org)

Snaps bounding box of selected 3D object(s) to bounds of other objects in the scene graph.

![demo.gif](screenshots/demo.gif)

</div>

> [!WARNING]
> Plugin is written in C# and requires the Godot .NET (Mono) 4.2+ version of the editor.

> [!NOTE]
> It's not vertex polygonal snapping that [already ships with Godot](https://github.com/godotengine/godot/pull/117235).

### Supported Nodes

* **Geometry instances**: `MeshInstance3D`, CSG shapes, `MultiMeshInstance3D`, etc.
* **Collision shapes**: `CollisionShape3D` (Box, Sphere, Cylinder, Capsule shapes).
* **Hierarchies/Groups**: works on `StaticBody3D`, `.tscn` packed scenes, and basic `Node3D` containers by auto-evaluating their children.

## Usage

1. Toggle the tool (● button) in the 3D viewport header.
2. Select your target 3D object(s).
3. Hold the <kbd>B</kbd> key and hover near a bounding box corner.
4. Click and hold the <kbd>LMB</kbd> on the corner to begin dragging.
5. Drag towards another object's bounding box corners to snap.
6. Release the mouse button to apply, or <kbd>RMB</kbd> while dragging to cancel.

## TODO

1. Concave/Convex collision shapes bounds snapping.


## License

MIT license (LICENSE-MIT or http://opensource.org/licenses/MIT)
