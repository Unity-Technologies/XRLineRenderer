# VR Line Renderer
A VR-Optimized line renderer that is also capable of producing very inexpensive glow effects.  The VRLineRenderer mimics rendering with 3d capsules while only using two quads worth of geometry.

## Setup and usage

1. Place the VRLineRenderer folder into Assets\XR Utilities\VRLineRenderer in your project.

2. Add a VRLineRenderer or VRTrailRenderer component to your gameobject.  The interface is nearly identical to the built in Unity Line and Trail Renderers.

3. Create a new material using the VRLineRenderer shaders.  You can find some examples in VRLineRenderer\Materials

4. Apply this material to the mesh renderer of your VRLineRenderer or VRTrailRenderer.


## VRLineRenderer Shader
You will find five shader variants under the VRLineRenderer category.  Each of these corresponds to a shader blend mode.
Max Color and Min Color are the cheapest variants - if you are using the line renderer to mimic glow effects these variants also are stable in that color will not blow out.

Explanation of interesting shader parameters:
Line Rendering Levels - This allows control over the blend between the inner(most opaque/intense) part of the line and outer(transparent) area.  Adjust the level curve to 0 will give a very glow-like effect while setting the cruve to 1 will make the line completely solid.

Line Scaled by Depth - Turning this option off means the line will stay the same thickness regardless of your distance from it.  This is excellent for drafting lines and also for simulating glow.  Radius minimum and maximum allow you to clamp this size adjustment.


## Custom VR Line Rendering
The Scripts\Meshchain class provides everything you need to make your own custom line rendering constructs.  VRLineRenderer and VRTrailRenderer emulate what the classic Unity components provide, but there are many more use cases out there.


### Project Settings
If you plan on making changes to The VR Line Renderer and/or contributing back, then you'll need to set the `Asset Serialization` property under Edit->Project Settings->Editor to `Force Text`