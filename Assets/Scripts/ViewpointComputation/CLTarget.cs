/*
-----------------------------------------------------------------------------
This source file is part of ViewpointComputationLib (a viewpoint computation library)
For more info on the project, contact Roberto Ranon at roberto.ranon@uniud.it.

Copyright (c) 2013- University of Udine, Italy - http://hcilab.uniud.it
-----------------------------------------------------------------------------

 CLTarget.cs: file defining classes for representing targets in a VC problem and to compute their 
 visual properties wrt to a camera (size, occlusion, etc. )

-----------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;


/// <summary>
/// Defines a generic target of our viewpoint computation problem
/// </summary>
public class CLTarget
{

	/// <summary>
	/// The Unity game object to which the target corresponds. 
	/// </summary>    
	public GameObject gameObject;


	/// <summary>
	/// The Unity game objects that contain renderables to which the target refers to. We use this to compute the AABB of the target.
	/// They might not strictly correspond to all renderables associated to the main object - only the ones we use for the target AABB
	/// </summary>    
	public List<GameObject> renderables;


	/// <summary>
	/// The Unity game objects that contain colliders to which the target refers to. We use this to check occlusions, and also to 
	/// compute AABB if useRendererForBoundingBoxes is false.
	/// </summary>    
	public List<GameObject> colliders;


	/// <summary>
	/// This list contains, for each collider in the colliders' list, the corresponding layer. We need this because we change layers
	/// during visibility evaluation.
	/// </summary>
	public List<int> collidersLayers=new List<int> ();


	/// <summary>
	/// world-space AABB of the target
	/// </summary>  
	public Bounds boundingBox;


	/// <summary>
	/// Name of the target
	/// </summary>  
	public string name;


	/// <summary>
	/// Radius of the bounding sphere of the target (center is the center of targetAABB).
	/// </summary>  
	public float radius;


	/// <summary>
	/// List of ground properties in which the target appears
	/// </summary>  
	public List<CLGroundProperty> groundProperties = new List<CLGroundProperty> ();


	/// <summary>
	/// True if the target has been rendered (in case we need to evaluate multiple properties for the target,
	/// no need to render it more than once)
	/// </summary>  
	public bool rendered;


	/// <summary>
	/// Number of visible vertices of the target AABB. Computed by render method for a specific camera.
	/// </summary>  
	private int numVisibleBBVertices;


	/// <summary>
	/// Visible vertices of the target AABB. Computed by render method for a specific camera.
	/// </summary>  
	public List<Vector3> visibleBBVertices = new List<Vector3> (10);


	/// <summary>
	/// Visible vertices of the AABB, projected. Computed by render method for a specific camera.
	/// </summary>  
	private List<Vector2> screenRepresentation = new List<Vector2> (10);


	/// <summary>
	/// Screen-space 2D AABB (in viewport coordinates) of the projection of the target. 
	/// Computed by render method for a specific camera.
	/// </summary>  
	public Rectangle screenAABB;


	/// <summary>
	/// Area (in viewport coordinates) of the projected AABB. 
	/// Computed by render method for a specific camera.
	/// </summary>  
	public float screenArea;


	/// <summary>
	/// How much the target is in screen, i.e. area(viewport-clipped projection of the target)/area(projection of the target). 
	/// Computed by render method for a specific camera.
	/// </summary>  
	public float screenRatio;


	/// <summary>
	/// how many rays to use for ray casting
	/// </summary>  
	private int nRays;


	/// <summary>
	/// bit mask for ray casting with 0s in correspondance of layers to ignore
	/// </summary>  
	private int layerMask;


	/// <summary>
	/// List of points that can be used for visibility checking
	/// </summary>
	public List<Vector3> visibilityPoints;


	/// <summary>
	/// List of points that WILL be used for visibility checking. These are a subset of
	/// visibilityPoints, already in world space
	/// </summary>
	public List<Vector3> actualVisibilityPoints=new List<Vector3> ();


	/// <summary>
	/// Whether to use renderers (true) or colliders (false) to compute bounding boxes
	/// </summary>
	public bool useRenderersForSize;


	/// <summary>
	/// Visibility point generation method
	/// </summary>
	public enum VisibilityPointGenerationMethod
	{
		RANDOM,         // visibility points are randomly generated
		UNIFORM_IN_BB,  // visibility points are generated uniformly inside bounding box
		ON_MESH         // visibility points are generated uniformly on target mesh
	}


	/// <summary>
	/// The adopted visibility point generation method
	/// </summary>
	public VisibilityPointGenerationMethod visibilityPointGeneration;


	/// <summary>
	/// Initializes a new instance of the <see cref="CLTarget"/> class.
	/// </summary>
	public CLTarget() {



	}


	/// <summary>
	/// Initializes a new instance of the <see cref="CLTarget"/> class.
	/// </summary>
	/// <param name="layersToExclude">Layers to exclude for ray casting</param>
	/// <param name="sceneObj">Corresponding scene object.</param>
	/// <param name="_renderables">Renderables (list of objects from which AABB is computed). If empty, we use colliders.</param>
	/// <param name="_colliders">Colliders (list of objects to be used for ray casting)</param>
	/// <param name="_nRays">number of rays to use for checking visibility</param>
	public CLTarget (GameObject sceneObj, List<GameObject> _renderables, List<GameObject> _colliders, int layersToExclude = 1 << 2, 
		VisibilityPointGenerationMethod _visibilityPointGeneration = VisibilityPointGenerationMethod.ON_MESH, int _nRays = 8 )
	{

		gameObject = sceneObj;
		name = sceneObj.name;

		if ( _renderables.Count > 0 ) {
			renderables = new List<GameObject>( _renderables );
			useRenderersForSize = true;
		}
		else {
			useRenderersForSize = false;
		}
		colliders = new List<GameObject>( _colliders );
		nRays = _nRays;
		layerMask = ~layersToExclude;
		visibilityPointGeneration = _visibilityPointGeneration;


		foreach (GameObject g in colliders) {

			collidersLayers.Add (g.layer);

		}

		UpdateBounds ();

		PreComputeVisibilityPoints (false, Math.Max(2* colliders.Count, 50));

	}



	/// <summary>
	/// Precomputes a number of visibility points inside the target BB, to be used later for visibility checking.
	/// Visibility points are generated according to the chosen method (value of visibilityPointGeneration member)
	/// We generate 50 visibility points (this could be a parameter ...)
	/// </summary>
	/// <param name="standingOnGround">If set to <c>true</c> standing on ground.</param>
	private void PreComputeVisibilityPoints( bool standingOnGround, int numberofPoints = 50 ) {

		visibilityPoints = new List<Vector3> (numberofPoints);


		switch (visibilityPointGeneration) {

		case VisibilityPointGenerationMethod.ON_MESH:
			{

				// remove everything in scene except target (it should be enough to simply move the target to a special layer)

				foreach (GameObject go in colliders) {
					go.layer = LayerMask.NameToLayer ("CameraControl");
				}

				// find target center, and proper distance such that target is entirely on screen from every angle
				float d = radius / Mathf.Tan( 50.0f); // supposing a h-fow of 100

				// compute n points on unit sphere 
				List<Vector3> samples = TargetUtils.ComputePointsOnSphere( numberofPoints, boundingBox.center, radius );

				// for visibility: cast 1 ray from each point to center, save point of intersection
				foreach (Vector3 point in samples) {

					RaycastHit hitPoint;
					bool hit = Physics.Linecast (point, boundingBox.center, out hitPoint, 1 << LayerMask.NameToLayer ("CameraControl"));

					if (hit) {
						visibilityPoints.Add (hitPoint.point);

					}

				}



				break;

			}

		case VisibilityPointGenerationMethod.UNIFORM_IN_BB:
			{ 
				// We take the AABB, and choose a number of points inside it . For simplicity, we allow only an odd number of rays. 
				// For more than 9 points, we move to random generation

				visibilityPoints.Add (boundingBox.center);

				if (numberofPoints > 1 && numberofPoints < 10) {  // add two points along the longest dimension of the AABB

					float[] extents = new float[]{ boundingBox.extents.x, boundingBox.extents.y, boundingBox.extents.z };
					int[] indices = new int[]{ 0, 1, 2 };
					Array.Sort (extents, indices);
					int longest = indices [2];
					int secondLongest = indices [1];
					int shortest = indices [0];

					Vector3 p1 = boundingBox.center;
					p1 [longest] = 0.25f * boundingBox.min [longest] + 0.75f * boundingBox.max [longest];
		
					Vector3 p2 = boundingBox.center;
					p2 [longest] = 0.75f * boundingBox.min [longest] + 0.25f * boundingBox.max [longest];

					if (numberofPoints > 3) {

						Vector3 p3 = boundingBox.center;
						p3 [secondLongest] = 0.75f * boundingBox.min [secondLongest] + 0.25f * boundingBox.max [secondLongest];

						Vector3 p4 = boundingBox.center;
						p4 [secondLongest] = 0.25f * boundingBox.min [secondLongest] + 0.75f * boundingBox.max [secondLongest];

						if (numberofPoints > 5) {

							p1 [secondLongest] = 0.25f * boundingBox.min [secondLongest] + 0.75f * boundingBox.max [secondLongest];
							p1 [shortest] = 0.25f * boundingBox.min [shortest] + 0.75f * boundingBox.max [shortest];

							p2 [secondLongest] = 0.25f * boundingBox.min [secondLongest] + 0.75f * boundingBox.max [secondLongest];
							p2 [shortest] = 0.25f * boundingBox.min [shortest] + 0.75f * boundingBox.max [shortest];

							p3 [shortest] = 0.25f * boundingBox.min [shortest] + 0.75f * boundingBox.max [shortest];

							p4 [shortest] = 0.75f * boundingBox.min [shortest] + 0.25f * boundingBox.max [shortest];

							Vector3 p5 = boundingBox.center;
							p5 [longest] = 0.25f * boundingBox.min [longest] + 0.75f * boundingBox.max [longest];
							p5 [secondLongest] = 0.75f * boundingBox.min [secondLongest] + 0.25f * boundingBox.max [secondLongest];
							p5 [shortest] = 0.75f * boundingBox.min [shortest] + 0.25f * boundingBox.max [shortest];

							Vector3 p6 = boundingBox.center;
							p6 [longest] = 0.75f * boundingBox.min [longest] + 0.25f * boundingBox.max [longest];
							p6 [secondLongest] = 0.75f * boundingBox.min [secondLongest] + 0.25f * boundingBox.max [secondLongest];
							p6 [shortest] = 0.75f * boundingBox.min [shortest] + 0.25f * boundingBox.max [shortest];

							if (numberofPoints == 9) {
								
								p3 [longest] = 0.75f * boundingBox.min [longest] + 0.25f * boundingBox.max [longest];

								p4 [longest] = 0.25f * boundingBox.min [longest] + 0.75f * boundingBox.max [longest];

								Vector3 p7 = boundingBox.center;
								p7 [longest] = 0.25f * boundingBox.min [longest] + 0.75f * boundingBox.max [longest];
								p7 [secondLongest] = 0.75f * boundingBox.min [secondLongest] + 0.25f * boundingBox.max [secondLongest];
								p7 [shortest] = 0.25f * boundingBox.min [shortest] + 0.75f * boundingBox.max [shortest];

								Vector3 p8 = boundingBox.center;
								p8 [longest] = 0.75f * boundingBox.min [longest] + 0.25f * boundingBox.max [longest];
								p8 [secondLongest] = 0.25f * boundingBox.min [secondLongest] + 0.75f * boundingBox.max [secondLongest];
								p8 [shortest] = 0.75f * boundingBox.min [shortest] + 0.25f * boundingBox.max [shortest];

								visibilityPoints.Add (p7);
								visibilityPoints.Add (p8);

							}

							visibilityPoints.Add (p5);
							visibilityPoints.Add (p6);

						}

						visibilityPoints.Add (p3);
						visibilityPoints.Add (p4);

					}


					visibilityPoints.Add (p1);
					visibilityPoints.Add (p2);
				} else {

					visibilityPointGeneration = VisibilityPointGenerationMethod.RANDOM;

				}
			
				break; 

			}

		case ( VisibilityPointGenerationMethod.RANDOM ):
			{  // random generation, corresponds to VisibilityPointGenerationMethod.RANDOM

				if (numberofPoints >= colliders.Count) {
					// assign 1 random point per collider, until they are over
					int colliderIndex = 0;
					while (numberofPoints > 0) {

						Vector3 newPoint = TargetUtils.RandomPointInsideBounds (colliders [colliderIndex].GetComponent<Collider> ().bounds);
						visibilityPoints.Add (newPoint);
						colliderIndex = (colliderIndex + 1) % colliders.Count;
						numberofPoints--;
					}
				} else {  // if we have more colliders than points, then let's consider the AABB of the target assign points inside it. Not ideal, but...
					
					while (numberofPoints > 0) {

						Vector3 newPoint = TargetUtils.RandomPointInsideBounds (boundingBox);
						visibilityPoints.Add (newPoint);
						numberofPoints--;
					}

				}

				break;

			}


		default:
			{

				Debug.Log ("Warning: NO visibility points computed for target " + name);
				break;
			}
		}


	}


	/// <summary>
	/// Computes and internally stores a screen representation of the target given the current camera. The screen
	/// representation is in turn used to reason about properties like size, framing, ...
	/// </summary>
	/// <param name="currentCamera">camera from which rendering is performed</param>
	/// <param name="performClipping">if true, we clip against the viewport. If not, screenArea can </param>
	public void Render (CLCameraMan camera, bool performClipping=true)
	{

		screenRepresentation.Clear ();
		visibleBBVertices.Clear ();

		/**
		 *   ok, so ... using the world-space AABB of the game object is not ideal because it appears it is the world AABB 
		 *   of the world-transformed local AABB of the mesh, see http://answers.unity3d.com/questions/292874/renderer-bounds.html
		 *   Also, using the AABB of the colliderMesh does not help (it appears to be identical to the renderer AABB)
		 * 
		 *   Could transform the camera to local space and use the AABB of the mesh ... 
		 */

		// world position of the camera
		Vector3 eye = camera.unityCamera.transform.position;

		Bounds AABB = boundingBox;

		// World-space min-max corners of the target's AABB
		Vector3 minCorner = AABB.min;
		Vector3 maxCorner = AABB.max;

		/** Calculate bit-string position to perform lookup in the table, real
        * spatial relationship is not relevant, the relevant information is the
        * vertex ordering */
		int pos = ((eye.x < minCorner.x ? 1 : 0) << (int)TargetUtils.RelativePositioning.LEFT)
			+ ((eye.x > maxCorner.x ? 1 : 0) << (int)TargetUtils.RelativePositioning.RIGHT)
			+ ((eye.y < minCorner.y ? 1 : 0) << (int)TargetUtils.RelativePositioning.BOTTOM)
			+ ((eye.y > maxCorner.y ? 1 : 0) << (int)TargetUtils.RelativePositioning.TOP)
			+ ((eye.z < minCorner.z ? 1 : 0) << (int)TargetUtils.RelativePositioning.FRONT)
			+ ((eye.z > maxCorner.z ? 1 : 0) << (int)TargetUtils.RelativePositioning.BACK);

		// If camera inside bounding box return 0
		numVisibleBBVertices = TargetUtils.Number (pos);
		if (numVisibleBBVertices == 0) {
			this.screenArea = 0.0f;
			screenAABB = new Rectangle (0.0f, 0.0f, 0.0f, 0.0f);
			screenRatio = 0.0f;
			return;
		}

		// Otherwise project vertices on screen
		// Array for storing projected vertices
		List<Vector2> projectedBBVertices = new List<Vector2> (10);

		bool behindCamera = false;

		// project each visibile vertex
		for (int i = 0; i < numVisibleBBVertices; i++) {

			Vector3 visibleVertex = TargetUtils.ReturnAABBVertex (TargetUtils.Vertex (i, pos), AABB);
			Vector3 projectedVertex = camera.unityCamera.WorldToViewportPoint (visibleVertex);
			if (projectedVertex.z >= 0) {
				projectedBBVertices.Add (projectedVertex);
				visibleBBVertices.Add (visibleVertex);
				//Debug.Log ( newPoint.ToString("F5"));
			} else {
				behindCamera = true;	
			}
		}

		// clip them by viewport

		if (performClipping) {
			TargetUtils.Clip (camera.clipRectangle, projectedBBVertices, screenRepresentation);
		}

		// if there are less than three vertices on screen, area is zero
		if (screenRepresentation.Count < 3) {
			this.screenArea = 0;
			screenAABB = new Rectangle (0.0f, 0.0f, 0.0f, 0.0f);
			screenRatio = 0.0f;
		} else {
			// compute area
			this.screenArea = Mathf.Min (TargetUtils.ComputeScreenArea (screenRepresentation), 1.0f);	

			// compute min and max vertices
			Vector2 minPoint = screenRepresentation [0];
			Vector2 maxPoint = screenRepresentation [1];


			for (int i = 0; i< screenRepresentation.Count; i++) {
				if (minPoint.x > screenRepresentation [i].x)
					minPoint.x = screenRepresentation [i].x;
				if (minPoint.y > screenRepresentation [i].y)
					minPoint.y = screenRepresentation [i].y;
				if (maxPoint.x < screenRepresentation [i].x)
					maxPoint.x = screenRepresentation [i].x;
				if (maxPoint.y < screenRepresentation [i].y)
					maxPoint.y = screenRepresentation [i].y;
			}

			screenAABB = new Rectangle (minPoint.x, maxPoint.x, minPoint.y, maxPoint.y);

			if (!behindCamera)
				screenRatio = this.screenArea / TargetUtils.ComputeScreenArea (projectedBBVertices);
			else 
				screenRatio = 0.5f;  // this is just a hack since otherwise bb projected points behind camera
			// are simply thrown away and the target, while partially on screen, 
			// could be considered entirely on screen

			if (screenRatio > 1.0f && performClipping) {
				screenRatio = 0.0f;
			}
			else if (screenRatio > 1.0f) {
				// this means we have no clipping and the projected AABB is greater than the viewport
				screenRatio = 1.0f;
			}


		}

	}


	/// <summary>
	/// Computes the ratio between the area of the projected target inside frame, and the area inside viewport
	/// </summary>  
	public float ComputeRatioInsideFrame (CLCameraMan camera, Rectangle frame)
	{
		if (this.screenArea < 0.00001)
			// target is outside viewport or too small
			return 0.0f;

		// clip screen representation by provided frame
		List< Vector2> partInFrame = new List<Vector2> (10);
		TargetUtils.Clip (frame, screenRepresentation, partInFrame);
		float framedArea = TargetUtils.ComputeScreenArea (partInFrame);
		return framedArea / this.screenArea;
	}


	/// <summary>
	/// Computes how much the target is occluded by other objects by shooting rays and checking intersections with colliders
	/// </summary>  
	public float ComputeOcclusion (CLCameraMan camera, bool frontBack = false, int _nRays = 0)
	{

		if (this.gameObject.layer == LayerMask.NameToLayer ("Overlay")) {
			return 0.0f;
		}

		float result = 0.0f;
		RaycastHit hitFront;
		RaycastHit hitBack;
		List<Vector3> points = new List<Vector3> ();
		int n = _nRays > 0? nRays: actualVisibilityPoints.Count;  
		// now move all colliders to layer 2 (ignore ray cast)
		foreach (GameObject go in colliders) {
			go.layer = 2;
		}


		for (int i = 0; i<n; i++) {
			Vector3 p = actualVisibilityPoints[i];
		
			bool isOccludedFront = Physics.Linecast (camera.unityCamera.transform.position, p, out hitFront, layerMask);

			if (isOccludedFront ) {
				result += 1.0f / n;

			} else if (frontBack) {

				bool isOccludedBack = Physics.Linecast (p, camera.unityCamera.transform.position, out hitBack, layerMask);
				if (isOccludedBack) {

					result += 1.0f / n;
				}
			}

		}

		int j=0;
		foreach (GameObject go in colliders) {
			go.layer = collidersLayers[j];
			j++;
		}

		return Mathf.Min (result, 1.0f);

	}

	/// <summary>
	/// Computes the min angle in radians between the provided v vector and one of the local axes of the target 
	/// </summary>
	public virtual float ComputeAngleWith (Vector3 v, TargetUtils.Axis axis)
	{
		Vector3 v2;
		switch (axis) {
		case TargetUtils.Axis.RIGHT:
			// we use the local coordinate system of the gameobject in gameObjectRef
			v2 = gameObject.transform.right;
			break;
		case TargetUtils.Axis.UP:
			v2 = gameObject.transform.up;
			break;
		case TargetUtils.Axis.WORLD_UP:
			v2 = Vector3.up;
			break;
		default: // forward
			v2 = gameObject.transform.forward;
			break;
		}

		return Vector3.Angle (v, v2);
	}


	public Vector3 computeRenderablesCentroid()
	{
		Vector3 result = new Vector3 (0.0f, 0.0f, 0.0f);
		foreach (GameObject go in renderables) {
			result = result + go.GetComponent<Renderer>().bounds.center;

		}
		return result / renderables.Count;


	}


	// this method generates a random viewpoint with more probability where
	// target properties will be satisfied. It assumes we have at least a size
	// property for the target, plus optional angle and occlusion properties

	public Vector3 GenerateRandomSatisfyingPosition (CLCameraMan camera, bool considerVisibility = false)
	{


		Vector3 result = new Vector3 ();

		bool found = false;

		int ntries = 0;

		float yFOV = (camera.cameraDomain.yFOVBounds [0] + camera.cameraDomain.yFOVBounds [1]) / 2;

		// we allow for 30 tries before giving up
		while (ntries < 30 && !found) {

			float distance = 0.0f;
			float phi = 0.0f;
			float theta=0.0f;

			foreach (CLGroundProperty p in groundProperties) {

				if (p is CLSizeProperty) {

					CLSizeProperty sp = (CLSizeProperty)p;

					// this returns a random area, or width, or height, depending on the type of size
					// property, with more probability where the satisfaction is higher
					float randomSize = p.satFunction.GenerateRandomXPoint ();
					if (randomSize < 0.0001f)
						randomSize = 0.0001f; // to avoid computing an infinite distance.

					// compute distance from target size
					distance = ComputeDistanceFromSize (randomSize, sp.sizeType, camera, yFOV);
						
				}

				if (p is CLOrientationProperty ) {
					CLOrientationProperty op = (CLOrientationProperty)p;

					if ( op.orientation == CLOrientationProperty.OrientationMode.HORIZONTAL ) {
						phi = Mathf.Deg2Rad * op.satFunction.GenerateRandomXPoint (); // horizontal random angle


					}
					else {
						theta = Mathf.Deg2Rad * op.satFunction.GenerateRandomXPoint (); // vertical random angle

					}
				}


			}

			// if we are not inside bs sphere
			if (distance > radius) {

				result = ComputeWorldPosFromSphericalCoordinates (distance, phi, theta);


				if (camera.InSearchSpace (new float[] {result.x, result.y, result.z}) ) {

					found = true;


				} 

			}

			ntries++;
		}


		if (!found) {
			float[] randomCandidate = camera.cameraDomain.ComputeRandomViewpoint (3);
			result = new Vector3 (randomCandidate [0], randomCandidate [1], randomCandidate [2]);
			//Debug.Log ("random candidate");
		} else {
			//Debug.Log ("smart candidate in " + ntries + " tries");
		}

		return result;
	}



	/// <summary>
	/// Given a desired on screen size (area, width, or height), computes camera distance, assuming the target is
	/// approximated by its bounding sphere, centered on the screen.
	/// </summary>
	/// <returns>the camera distance</returns>
	/// <param name="targetSize">Target size.</param>
	/// <param name="sizeMode">Size mode.</param>
	/// <param name="camera">Camera.</param>
	/// <param name="yFOV">yFOV, in degrees</param>
	public float ComputeDistanceFromSize (float targetSize, CLSizeProperty.SizeMode sizeMode, CLCameraMan camera, float yFOV) {

		// AR is width/height
		float AR = camera.unityCamera.aspect;
		// we compute horizontal FOV
		float yFOVRad = Mathf.Deg2Rad * yFOV;

		float projectedRadius = 1.0f;

		// now we need to compute distance from size
		if (sizeMode == CLSizeProperty.SizeMode.AREA) {

			// assuming viewport height is 1, area viewport is 1*AR. Our target area is therefore relative to targetSize*AR 
			// projected radius should then be
			projectedRadius = Mathf.Sqrt( targetSize * AR / Mathf.PI);

		} else if (sizeMode == CLSizeProperty.SizeMode.HEIGHT) {

			// assuming viewport height is 1, our target height is correct (relative to 1). We need half height
			projectedRadius = 0.5f * targetSize;

		} else { // it is a width property

			// assuming viewport height is 1, our target width is relative to targetSize*AR
			projectedRadius = 0.5f * targetSize*AR;
		}

		// this means, in world space, the distance from the center of the sphere to the top of the screen should be
		float halfscreen = radius * 0.5f / projectedRadius;

		// now solve with the usual trigonometry relation

		float distance = halfscreen / Mathf.Tan (yFOVRad / 2);

		return distance;
	}

	/// <summary>
	/// Computes the world position from spherical coordinates.
	/// </summary>
	/// <returns>The world position from spherical coordinates.</returns>
	/// <param name="distance">Distance.</param>
	/// <param name="phi">Phi.</param>
	/// <param name="theta">polar angle theta, 0 is the north pole, value in radians</param>
	public Vector3 ComputeWorldPosFromSphericalCoordinates( float distance, float phi, float theta) {

		Vector3 result;

		result.x = distance * Mathf.Sin (theta) * Mathf.Sin (phi);
		result.y = distance * Mathf.Cos (theta);
		result.z = distance * Mathf.Cos (phi) * Mathf.Sin (theta); 

		Vector3 shift = boundingBox.center - gameObject.transform.position;
		//Debug.Log (shift.magnitude);

		// now check that we are inside bounds
		// invert scale 
		Vector3 scaledResult = new Vector3 (1 / gameObject.transform.lossyScale.x,
			1 / gameObject.transform.lossyScale.y,
			1 / gameObject.transform.lossyScale.z);

		result = gameObject.transform.TransformPoint (Vector3.Scale(result, scaledResult)) + shift;

		return result;
	}


	/// <summary>
	/// Updates the AABB and BS based on provided AABB, or computes them from renderables / collliders bounds
	/// </summary>
	/// <returns>The bounds.</returns>
	public Bounds UpdateBounds( Bounds b) {

		boundingBox = b;
		UpdateVisibilityInfo ( false );
		radius = boundingBox.extents.magnitude;
		return boundingBox;

	}


	/// <summary>
	/// Updates the AABB and BS based on provided AABB, or computes them from renderables / collliders bounds
	/// </summary>
	/// <returns>The bounds.</returns>
	public Bounds UpdateBounds ()
	{
		
		if (useRenderersForSize) {

			Bounds targetBounds = new Bounds (renderables [0].GetComponent<Renderer> ().bounds.center, renderables [0].GetComponent<Renderer> ().bounds.size);
			foreach (GameObject renderable in renderables.Skip(1 )) {
				targetBounds.Encapsulate (renderable.GetComponent<Renderer> ().bounds);
			}

			boundingBox = targetBounds;

		} else {

			Bounds targetBounds = new Bounds (colliders [0].GetComponent<Collider> ().bounds.center, colliders [0].GetComponent<Collider> ().bounds.size);
			foreach (GameObject collider in colliders.Skip(1 )) {
				targetBounds.Encapsulate (collider.GetComponent<Collider> ().bounds);
			}
					
			boundingBox = targetBounds;

		}

		radius = boundingBox.extents.magnitude;
	
		return boundingBox;
	}


	public void UpdateVisibilityInfo ( bool transformChanged ) {

		actualVisibilityPoints.Clear ();

		// this should transform the chosen visibility points if there was some change in the target (e.g. some change of
		// transformation 

		// for the moment, let's suppose there are no changes
		if (visibilityPointGeneration == VisibilityPointGenerationMethod.RANDOM ||
		    visibilityPointGeneration == VisibilityPointGenerationMethod.ON_MESH) {

			while (actualVisibilityPoints.Count < nRays) {

				actualVisibilityPoints.Add (visibilityPoints [UnityEngine.Random.Range (0, visibilityPoints.Count)]);


			}

		}


	}

}


