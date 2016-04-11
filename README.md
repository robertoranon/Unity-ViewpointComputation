# Unity-ViewpointComputation

Unity-ViewpointComputation is a library for computing the virtual camera parameters (position, orientation, FOV) that satisfy a set of visual requirements - such as size, visibility, and angle of a set of objects in the scene - in the image rendered by the camera. Instead of playing with camera parameters, compose an image by identifying subjects, their size, position, and visibility, and the library will compute the virtual camera parameters for you.

Examples:

- compute a virtual camera that allows the user to see a number of targets
- create classic compositions, such as over-the-shoulder shots, internal shots, and so on

The library can compute a solution camera in any chosen amount of time (e.g., 10 milliseconds). For complex problems, however, more time will in general translate into better solutions. It is also possible to split the computation between a few successive frames.

The library uses a stochastic solver based on Particle Swarm Optimization to find the best camera. A detailed exaplanation can be found in [Ranon, Urli, Improving the Efficiency of Viewpoint Computation, IEEE TVCG 2014](http://hcilab.uniud.it/publications/356.html). 

## Using the library

- Add the scripts in Scripts/ViewpointComputation to your project.
- All game objects in the scene that can block visibility must have a collider, since the library uses ray casting to measure
visibility.
- Add Scripts/VCTesting.cs to a camera in the scene, and modify it to your needs.

You are free to use the library in any research or commercial project, or any use allowed by the Apache License. If you use the library in any scientific publication, please cite:

Ranon R., Urli T.,	Improving the Efficiency of Viewpoint Composition, IEEE Transactions on Visualization and Computer Graphics, 20(5), May 2014, pp. 795-807.





