# Unity-ViewpointComputation

Unity-ViewpointComputation is a library for computing the virtual camera parameters (position, orientation, FOV) that satisfy a set of visual requirements - such as size, visibility, and angle of a set of objects in the scene - in the image rendered by the camera. Instead of playing with camera parameters, compose an image by identifying subjects, their size, position, and visibility, and the library will compute the virtual camera parameters for you.

The library has been tested with Unity 5.3, but should work also with older versions (and even with 5.4 beta).

Example usages:

- compute a virtual camera that allows the user to see a number of targets
- create classic compositions, such as over-the-shoulder shots, internal shots, and so on
- evaluate how much a unity camera satisfies a set of visual properties, e.g. evaluate if certain objects are visible by the camera or how big they are on screen.
- improve Unity Editor "Frame Selected" command with visibility-awareness, as described [here](http://hcilab.uniud.it/images/stories/publications/2015-06/VisibilityFraming_WICED2015.pdf).

The library can compute a solution camera in any chosen amount of time (e.g., 10 milliseconds). For complex problems, however, more time will in general translate into better solutions. It is also possible to split the computation between a few successive frames.

The library uses a stochastic solver based on Particle Swarm Optimization to find the best camera. A detailed explanation can be found in [Ranon, Urli, Improving the Efficiency of Viewpoint Computation, IEEE TVCG 2014](http://hcilab.uniud.it/publications/356.html). 

## Using the library

- Add the scripts in Scripts/ViewpointComputation to your project.
- All game objects in the scene that can block visibility must have a collider, since the library uses ray casting to measure
visibility.
- Add Scripts/VCTesting.cs to a camera in the scene, and modify it to your needs.

You are free to use the library in research or commercial projects, or any use allowed by the Apache License. If you use the library in any scientific publication, please cite:

Ranon R., Urli T.,	Improving the Efficiency of Viewpoint Composition, IEEE Transactions on Visualization and Computer Graphics, 20(5), May 2014, pp. 795-807.

## Example Scene

An example scene (as a .unitypackage) is provided [here](https://www.dropbox.com/s/u31imdepcbb9fqw/Office-VC.unitypackage?dl=0) and represents an office with four characters. After importing the package into your project, open "VCDemoScene". In the scene, the "Main Camera" object has a VCTesting script component which allows one to define the targets that we want to frame. In the example scene, the predefined targets are "BookShelf", "Matteo", and "Chair09". Code in VCTesting.cs then defines, for each target, a number of visual properties that should be satisfied, namely desired values for size, visibility, and angle with the camera.

After playing the scene, press "p" to compute and show a camera that satisifies these properties, and "e" to evaluate how much the current camera satisfies the same properties (results are shown in the console). As you can see, each time you press "p" a different camera is generated. This is due to the stochastic nature of the solving process. 

While in the Editor scene view, you can test our modified "Visibility-Aware Framing" command by selecting any object and then pressing the 'h' key. The editor scene camera will then be modified to frame the selected objects similarly to the normal "Frame Selected" Unity command, but also rotating the editor camera to ensure visibility of the selected objects.

 





