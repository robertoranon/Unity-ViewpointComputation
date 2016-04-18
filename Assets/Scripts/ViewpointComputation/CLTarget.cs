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
	public GameObject gameObjectRef;


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
	/// Screen-space 2D AABB (in viewport coordinates) of the projection of the target (its AABB). 
	/// Computed at solving time for a specific camera.
	/// </summary>  
	public Rectangle screenSpaceAABB;


	/// <summary>
	/// Area (in viewport coordinates) of the projected AABB. 
	/// Computed at solving time for a specific camera.
	/// </summary>  
	public float screenArea;


	/// <summary>
	/// How much the target is in screen, i.e. area(viewport-clipped projection of the target)/area(projection of the target). 
	/// Computed at solving time for a specific camera.
	/// </summary>  
	public float inScreenRatio;


	/// <summary>
	/// AABB of the target
	/// </summary>  
	public Bounds targetAABB;


	/// <summary>
	/// Name of the target
	/// </summary>  
	public string name;


	/// <summary>
	/// Radius of the bounding sphere of the target (center is the center of targetAABB).
	/// </summary>  
	public float radius;


	/// <summary>
	/// Contribution of the target to properties satisfaction. Computed at solving time for a specific problem.
	/// </summary>  
	public float contribution;


	/// <summary>
	/// Cost to evaluate the properties that refer to the target. Computed at solving time for a specific problem.
	/// </summary>  
	public float evaluationCost;


	/// <summary>
	/// List of ground properties in which the target appears
	/// </summary>  
	public List<CLGroundProperty> targetProperties;


	/// <summary>
	/// True if the target has been rendered (in case we need to evaluate multiple properties for the target,
	/// no need to render it more than once)
	/// </summary>  
	public bool rendered;


	/// <summary>
	/// Number of visible vertices of the target AABB. Computed at solving time for a specific camera.
	/// </summary>  
	private int numVisibleBBVertices;

	/// <summary>
	/// Visible vertices of the AABB, projected. Computed at solving time for a specific camera.
	/// </summary>  
	private List<Vector2> screenRepresentation;

	/// <summary>
	/// Sets the target AABB, and computes bounding sphere radius
	/// </summary>  
	/// <param name='AABB'>new AABB.</param>
	public void SetBoundingVolume (Bounds AABB)
	{
		targetAABB = AABB;
		radius = AABB.extents.magnitude;
		screenRepresentation = new List<Vector2> (10);
	}
		
	/// <summary>
	/// how many rays to use for ray casting
	/// </summary>  
	private int nRays;

	/// <summary>
	/// bit mask for ray casting with 0s in correspondance of layers to ignore
	/// </summary>  
	private int layerMask;

	/// <summary>
	/// List of points to be used for visibility checking
	/// </summary>
	private List<Vector3> visibilityPoints;

	/// <summary>
	/// Whether to use renderers (true) or colliders (false) to compute bounding boxes
	/// </summary>
	public bool useRendererForBoundingBoxes;

	/// <summary>
	/// Initializes a new instance of the <see cref="CLTarget"/> class.
	/// </summary>
	public CLTarget() {}

	/// <summary>
	/// Initializes a new instance of the <see cref="CLTarget"/> class.
	/// </summary>
	/// <param name="layersToExclude">Layers to exclude for ray casting</param>
	/// <param name="sceneObj"> Corresponding scene object.</param>
	/// <param name="_renderables">Renderables (list of objects from which AABB is computed)</param>
	/// <param name="_colliders">Colliders (list of objects to be used for ray casting)</param>
	/// <param name="_useRendererForBoundingBoxes">If set to <c>true</c> use renderer for bounding boxes, otherwise we use colliders</param>
	/// <param name="_nRays">number of rays to use for checking visibility</param>
	public CLTarget (int layersToExclude, GameObject sceneObj, List<GameObject> _renderables, List<GameObject> _colliders, bool _useRendererForBoundingBoxes = true, int _nRays = 8 )
	{

		layerMask = ~layersToExclude;
		renderables = new List<GameObject>( _renderables );
		colliders = new List<GameObject>( _colliders );
		targetProperties = new List<CLGroundProperty> ();
		gameObjectRef = sceneObj;
		nRays = _nRays;
		useRendererForBoundingBoxes = _useRendererForBoundingBoxes;
		name = sceneObj.name;
		// builds list of points to be used for ray casting
		visibilityPoints = new List<Vector3>(nRays);
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

		/**
		 *   ok, so ... using the world-space AABB of the game object is not ideal because it appears it is the world AABB 
		 *   of the world-transformed local AABB of the mesh, see http://answers.unity3d.com/questions/292874/renderer-bounds.html
		 *   Also, using the AABB of the colliderMesh does not help (it appears to be identical to the renderer AABB)
		 * 
		 *   Could transform the camera to local space and use the AABB of the mesh ... 
		 */

		// world position of the camera
		Vector3 eye = camera.unityCamera.transform.position;

		Bounds AABB = targetAABB;

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
			screenSpaceAABB = new Rectangle (0.0f, 0.0f, 0.0f, 0.0f);
			inScreenRatio = 0.0f;
			return;
		}

		// Otherwise project vertices on screen
		// Array for storing projected vertices
		List<Vector2> projectedBBVertices = new List<Vector2> (10);

		bool behindCamera = false;

		// project each visibile vertex
		for (int i = 0; i < numVisibleBBVertices; i++) {

			Vector3 newPoint = camera.unityCamera.WorldToViewportPoint (TargetUtils.ReturnAABBVertex (TargetUtils.Vertex (i, pos), AABB));
			if (newPoint.z >= 0) {
				projectedBBVertices.Add (newPoint);
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
			screenSpaceAABB = new Rectangle (0.0f, 0.0f, 0.0f, 0.0f);
			inScreenRatio = 0.0f;
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

			screenSpaceAABB = new Rectangle (minPoint.x, maxPoint.x, minPoint.y, maxPoint.y);

			if (!behindCamera)
				inScreenRatio = this.screenArea / TargetUtils.ComputeScreenArea (projectedBBVertices);
			else 
				inScreenRatio = 0.5f;  // this is just a hack since otherwise bb projected points behind camera
			// are simply thrown away and the target, while partially on screen, 
			// could be considered entirely on screen

			if (inScreenRatio > 1.0f && performClipping) {
				inScreenRatio = 0.0f;
			}
			else if (inScreenRatio > 1.0f) {
				// this means we have no clipping and the projected AABB is greater than the viewport
				inScreenRatio = 1.0f;
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
	/// Computes how much the target is occluded by other objects by shooting N rays randomly inside the AABB of the target.
	/// Current strategy is 1 ray to the center plus nRays-1 random rays
	/// </summary>  
	public float ComputeOcclusion (CLCameraMan camera, bool frontBack = true)
	{

		float result = 0.0f;
		RaycastHit hitFront;
		RaycastHit hitBack;
		List<Vector3> points = new List<Vector3> ();
		int n = nRays;  
		for (int i = 0; i<n; i++) {
				points.Add (visibilityPoints[i]);
			}

		// now raycast from camera to each point
		foreach ( Vector3 p in points )
		{
			bool isOccludedFront = Physics.Linecast (camera.unityCamera.transform.position, p, out hitFront, layerMask);

			if (frontBack)
			{
				bool isOccludedBack = Physics.Linecast (p, camera.unityCamera.transform.position, out hitBack, layerMask);
				if (( isOccludedFront && ( !colliders.Contains (hitFront.collider.gameObject) ) ) ||  // if ray hit something, but not any of the target colliders
					( isOccludedBack && ( !colliders.Contains (hitBack.collider.gameObject) ) ) ) 	
				{
					result += 1.0f/ n;

				}
			}
			else {
				if (( isOccludedFront && ( !colliders.Contains (hitFront.collider.gameObject))))	
				{
					result += 1.0f/ n;

				}

			}

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
			v2 = gameObjectRef.transform.right;
			break;
		case TargetUtils.Axis.UP:
			v2 = gameObjectRef.transform.up;
			break;
		case TargetUtils.Axis.WORLD_UP:
			v2 = Vector3.up;
			break;
		default: // forward
			v2 = gameObjectRef.transform.forward;
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

		// we operate in spherical coordinates, and we start with random values
		float distance = 300.0f * UnityEngine.Random.value;   // instead of 300.0f, we should use the position bounds diagonal
		float theta = (Mathf.PI) * UnityEngine.Random.value;
		float phi = Mathf.PI * 2 * UnityEngine.Random.value; 

		Vector3 result = new Vector3 ();

		bool found = false;

		// we allow for 30 tries before giving up
		for (int i = 0; i<30 && !found; i++) {

			foreach (CLGroundProperty p in targetProperties) {

				if (p is CLSizeProperty) {
					float FOV;

					Vector2 yFOVrange = camera.cameraDomain.yFOVBounds;
					float yFOV = (yFOVrange [1] + yFOVrange [0]) / 2;  // required vertical FOV
					// AR is width/height
					float AR = camera.unityCamera.aspect;
					// we compute horizontal FOV
					float xFOV = AR * yFOV;

					// assuming AREA property. We convert areas to radiuses, and use the min of the two FOVs
					// XXXX we need to handle also WIDTH and HEIGHT
					FOV = Mathf.Min (xFOV, yFOV);

					float tmp = 0.3f / Mathf.Tan (Mathf.Deg2Rad * FOV / 2);   // should be 0.5, but bs is bigger than AABB

					float randomSize = p.satFunction.GenerateRandomXPoint ();

					// convert x, which is a size, to a distance from camera

					if (randomSize < 0.0001f)
						randomSize = 0.0001f; // to avoid computing an infinite distance.


					// assuming AREA property. We convert areas to radiuses, and use the average of the two FOVs
					randomSize = Mathf.Sqrt (randomSize / Mathf.PI);
					distance = (radius / randomSize) * tmp;  // now x is a distance (instead of width, height or area)
				}

				if (p is CLOrientationProperty ) {
					CLOrientationProperty op = (CLOrientationProperty)p;

					if ( op.orientation == CLOrientationProperty.OrientationMode.HORIZONTAL ) {
						phi = Mathf.Deg2Rad * op.satFunction.GenerateRandomXPoint (); // horizontal random angle
						// XXX we are not considering CameraOrientation

					}
					else {
						theta = Mathf.Deg2Rad * op.satFunction.GenerateRandomXPoint (); // vertical random angle
						// XXX we are not considering CameraOrientation
					}
				}


			}

			// if we are not inside bs sphere
			if (distance > radius) {
				// convert in cartesian coordinates
				result.x = distance * Mathf.Sin (theta) * Mathf.Sin (phi);
				result.y = distance * Mathf.Cos (theta);
				result.z = distance * Mathf.Cos (phi) * Mathf.Sin (theta); 

				Vector3 shift = targetAABB.center - gameObjectRef.transform.position;

				// now check that we are inside bounds
				result = gameObjectRef.transform.TransformPoint (result) + shift;

				if (camera.inSearchSpace (new float[] {result.x, result.y, result.z})) {

					found = true;
				} 

			}
		}


		if (!found)
		{
			float[] randomCandidate = camera.cameraDomain.ComputeRandomViewpoint(3);
			result = new Vector3( randomCandidate[0], randomCandidate[1], randomCandidate[2]);
		}

		return result;
	}



	public Bounds UpdateBounds () 
	{

		Bounds result;

		if (useRendererForBoundingBoxes) {

			Bounds targetBounds = new Bounds (renderables [0].GetComponent<Renderer> ().bounds.center, renderables [0].GetComponent<Renderer> ().bounds.size);
			foreach (GameObject renderable in renderables.Skip(1 )) {
				targetBounds.Encapsulate (renderable.GetComponent<Renderer> ().bounds);
			}
			SetBoundingVolume (targetBounds);

			result = targetBounds;

		} else {

			Bounds targetBounds = new Bounds (colliders [0].GetComponent<Collider> ().bounds.center, colliders [0].GetComponent<Collider> ().bounds.size);
			foreach (GameObject collider in colliders.Skip(1 )) {
				targetBounds.Encapsulate (collider.GetComponent<Collider> ().bounds);
			}
			SetBoundingVolume (targetBounds);

			result = targetBounds;

		}

		// update visibility points
		visibilityPoints.Clear();
		// determine longest extent of AABB
		float[] extents = new float[]{ result.extents.x, result.extents.y, result.extents.z };
		int[] indices = new int[]{ 0, 1, 2 };
		Array.Sort (extents, indices);
		int longest = indices [0];
		int secondLongest = indices [1];
		int shortest = indices [2];

		// now, depending on the number of rays, determine visibility points
		if (nRays == 1) {
			visibilityPoints.Add (result.center);
		} else if (nRays == 2) { // two points, along the longest dimension of the AABBB
			Vector3 p1 = result.center;
			p1 [longest] = 0.33f * result.min [longest] + 0.66f * result.max [longest];
			visibilityPoints.Add (p1);
			Vector3 p2 = result.center;
			p2 [longest] = 0.66f * result.min [longest] + 0.33f * result.max [longest];
			visibilityPoints.Add (p2);
		} else if (nRays == 3) { // three points along the longest dimension of the AABB
			visibilityPoints.Add (result.center);
			Vector3 p1 = result.center;
			p1 [longest] = 0.25f * result.min [longest] + 0.75f * result.max [longest];
			visibilityPoints.Add (p1);
			Vector3 p2 = result.center;
			p2 [longest] = 0.75f * result.min [longest] + 0.25f * result.max [longest];
			visibilityPoints.Add (p2);

		} else if (nRays == 4) { // 4 points inside AABB
			Vector3 p1 = result.center;
			p1 [longest] = 0.33f * result.min [longest] + 0.66f * result.max [longest];
			p1 [secondLongest] = 0.33f * result.min [secondLongest] + 0.66f * result.max [secondLongest];
			visibilityPoints.Add (p1);

			Vector3 p2 = result.center;
			p2 [longest] = 0.66f * result.min [longest] + 0.33f * result.max [longest];
			p2 [secondLongest] = 0.33f * result.min [secondLongest] + 0.66f * result.max [secondLongest];
			visibilityPoints.Add (p2);

			Vector3 p3 = result.center;
			p3 [longest] = 0.66f * result.min [longest] + 0.33f * result.max [longest];
			p3 [secondLongest] = 0.66f * result.min [secondLongest] + 0.33f * result.max [secondLongest];
			visibilityPoints.Add (p3);

			Vector3 p4 = result.center;
			p4 [longest] = 0.33f * result.min [longest] + 0.66f * result.max [longest];
			p4 [secondLongest] = 0.66f * result.min [secondLongest] + 0.33f * result.max [secondLongest];
			visibilityPoints.Add (p4);
		} else if (nRays == 5) { // 

			visibilityPoints.Add (result.center);

			Vector3 p1 = result.center;
			p1 [longest] = 0.25f * result.min [longest] + 0.66f * result.max [longest];
			p1 [secondLongest] = 0.25f * result.min [secondLongest] + 0.75f * result.max [secondLongest];
			visibilityPoints.Add (p1);

			Vector3 p2 = result.center;
			p2 [longest] = 0.75f * result.min [longest] + 0.25f * result.max [longest];
			p2 [secondLongest] = 0.25f * result.min [secondLongest] + 0.75f * result.max [secondLongest];
			visibilityPoints.Add (p2);

			Vector3 p3 = result.center;
			p3 [longest] = 0.75f * result.min [longest] + 0.25f * result.max [longest];
			p3 [secondLongest] = 0.75f * result.min [secondLongest] + 0.25f * result.max [secondLongest];
			visibilityPoints.Add (p3);

			Vector3 p4 = result.center;
			p4 [longest] = 0.25f * result.min [longest] + 0.75f * result.max [longest];
			p4 [secondLongest] = 0.75f * result.min [secondLongest] + 0.25f * result.max [secondLongest];
			visibilityPoints.Add (p4);
		} else if (nRays == 6) {

			Vector3 p1 = result.center;
			p1 [0] = 0.33f * result.min [0] + 0.66f * result.max [0];
			p1 [1] = 0.33f * result.min [1] + 0.66f * result.max [1];
			visibilityPoints.Add (p1);

			Vector3 p2 = result.center;
			p2 [0] = 0.66f * result.min [0] + 0.33f * result.max [0];
			p2 [1] = 0.33f * result.min [1] + 0.66f * result.max [1];
			visibilityPoints.Add (p2);

			Vector3 p3 = result.center;
			p3 [0] = 0.33f * result.min [0] + 0.66f * result.max [0];
			p3 [2] = 0.33f * result.min [2] + 0.66f * result.max [2];
			visibilityPoints.Add (p3);

			Vector3 p4 = result.center;
			p4 [0] = 0.66f * result.min [0] + 0.33f * result.max [0];
			p4 [2] = 0.33f * result.min [2] + 0.66f * result.max [2];
			visibilityPoints.Add (p4);

			Vector3 p5 = result.center;
			p5 [1] = 0.33f * result.min [1] + 0.66f * result.max [1];
			p5 [2] = 0.33f * result.min [2] + 0.66f * result.max [2];
			visibilityPoints.Add (p5);

			Vector3 p6 = result.center;
			p6 [1] = 0.66f * result.min [1] + 0.33f * result.max [1];
			p6 [2] = 0.33f * result.min [2] + 0.66f * result.max [2];
			visibilityPoints.Add (p6);
		} else if (nRays == 7) {

			visibilityPoints.Add (result.center);

			Vector3 p1 = result.center;
			p1 [0] = 0.25f * result.min [0] + 0.75f * result.max [0];
			p1 [1] = 0.25f * result.min [1] + 0.75f * result.max [1];
			visibilityPoints.Add (p1);

			Vector3 p2 = result.center;
			p2 [0] = 0.75f * result.min [0] + 0.25f * result.max [0];
			p2 [1] = 0.25f * result.min [1] + 0.75f * result.max [1];
			visibilityPoints.Add (p2);

			Vector3 p3 = result.center;
			p3 [0] = 0.25f * result.min [0] + 0.75f * result.max [0];
			p3 [2] = 0.25f * result.min [2] + 0.75f * result.max [2];
			visibilityPoints.Add (p3);

			Vector3 p4 = result.center;
			p4 [0] = 0.75f * result.min [0] + 0.25f * result.max [0];
			p4 [2] = 0.25f * result.min [2] + 0.75f * result.max [2];
			visibilityPoints.Add (p4);

			Vector3 p5 = result.center;
			p5 [1] = 0.25f * result.min [1] + 0.75f * result.max [1];
			p5 [2] = 0.25f * result.min [2] + 0.75f * result.max [2];
			visibilityPoints.Add (p5);

			Vector3 p6 = result.center;
			p6 [1] = 0.75f * result.min [1] + 0.25f * result.max [1];
			p6 [2] = 0.25f * result.min [2] + 0.75f * result.max [2];
			visibilityPoints.Add (p6);

		} else if (nRays == 8) {

			Vector3 p1 = result.center;
			p1 [longest] = 0.33f * result.min [longest] + 0.66f * result.max [longest];
			p1 [secondLongest] = 0.33f * result.min [secondLongest] + 0.66f * result.max [secondLongest];
			p1 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p1);

			Vector3 p2 = result.center;
			p2 [longest] = 0.66f * result.min [longest] + 0.33f * result.max [longest];
			p2 [secondLongest] = 0.33f * result.min [secondLongest] + 0.66f * result.max [secondLongest];
			p2 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p2);

			Vector3 p3 = result.center;
			p3 [longest] = 0.66f * result.min [longest] + 0.33f * result.max [longest];
			p3 [secondLongest] = 0.66f * result.min [secondLongest] + 0.33f * result.max [secondLongest];
			p3 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p3);

			Vector3 p4 = result.center;
			p4 [longest] = 0.33f * result.min [longest] + 0.66f * result.max [longest];
			p4 [secondLongest] = 0.66f * result.min [secondLongest] + 0.33f * result.max [secondLongest];
			p4 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p4);

			Vector3 p5 = result.center;
			p5 [longest] = 0.33f * result.min [longest] + 0.66f * result.max [longest];
			p5 [secondLongest] = 0.33f * result.min [secondLongest] + 0.66f * result.max [secondLongest];
			p5 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p5);

			Vector3 p6 = result.center;
			p6 [longest] = 0.66f * result.min [longest] + 0.33f * result.max [longest];
			p6 [secondLongest] = 0.33f * result.min [secondLongest] + 0.66f * result.max [secondLongest];
			p6 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p6);

			Vector3 p7 = result.center;
			p7 [longest] = 0.66f * result.min [longest] + 0.33f * result.max [longest];
			p7 [secondLongest] = 0.66f * result.min [secondLongest] + 0.33f * result.max [secondLongest];
			p7 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p7);

			Vector3 p8 = result.center;
			p8 [longest] = 0.33f * result.min [longest] + 0.66f * result.max [longest];
			p8 [secondLongest] = 0.66f * result.min [secondLongest] + 0.33f * result.max [secondLongest];
			p8 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p8);

		} else if (nRays == 9) {

			visibilityPoints.Add (result.center);

			Vector3 p1 = result.center;
			p1 [longest] = 0.33f * result.min [longest] + 0.75f * result.max [longest];
			p1 [secondLongest] = 0.25f * result.min [secondLongest] + 0.75f * result.max [secondLongest];
			p1 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p1);

			Vector3 p2 = result.center;
			p2 [longest] = 0.75f * result.min [longest] + 0.25f * result.max [longest];
			p2 [secondLongest] = 0.25f * result.min [secondLongest] + 0.75f * result.max [secondLongest];
			p2 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p2);

			Vector3 p3 = result.center;
			p3 [longest] = 0.75f * result.min [longest] + 0.25f * result.max [longest];
			p3 [secondLongest] = 0.75f * result.min [secondLongest] + 0.25f * result.max [secondLongest];
			p3 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p3);

			Vector3 p4 = result.center;
			p4 [longest] = 0.25f * result.min [longest] + 0.75f * result.max [longest];
			p4 [secondLongest] = 0.75f * result.min [secondLongest] + 0.25f * result.max [secondLongest];
			p4 [shortest] = 0.33f * result.min [shortest] + 0.66f * result.max [shortest];
			visibilityPoints.Add (p4);

			Vector3 p5 = result.center;
			p5 [longest] = 0.25f * result.min [longest] + 0.75f * result.max [longest];
			p5 [secondLongest] = 0.25f * result.min [secondLongest] + 0.75f * result.max [secondLongest];
			p5 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p5);

			Vector3 p6 = result.center;
			p6 [longest] = 0.75f * result.min [longest] + 0.25f * result.max [longest];
			p6 [secondLongest] = 0.25f * result.min [secondLongest] + 0.75f * result.max [secondLongest];
			p6 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p6);

			Vector3 p7 = result.center;
			p7 [longest] = 0.75f * result.min [longest] + 0.25f * result.max [longest];
			p7 [secondLongest] = 0.75f * result.min [secondLongest] + 0.25f * result.max [secondLongest];
			p7 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p7);

			Vector3 p8 = result.center;
			p8 [longest] = 0.25f * result.min [longest] + 0.75f * result.max [longest];
			p8 [secondLongest] = 0.75f * result.min [secondLongest] + 0.25f * result.max [secondLongest];
			p8 [shortest] = 0.66f * result.min [shortest] + 0.33f * result.max [shortest];
			visibilityPoints.Add (p8);


		} else if (nRays > 9) {   // random distribution

			visibilityPoints.Add (result.center);
			// now add nRays -1 random points inside AABB to list
			for (int i = 0; i<( nRays - 1); i++) {
				visibilityPoints.Add (new Vector3 (UnityEngine.Random.Range (result.min.x, result.max.x),
					UnityEngine.Random.Range (result.min.y, result.max.y),
					UnityEngine.Random.Range (result.min.z, result.max.z)));
			}

		}

		return (result);

	}
		

}


