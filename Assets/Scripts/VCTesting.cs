/*
 * -------------------------------------------------------------------------------------
 * This source file is part of ViewpointComputationLib (a viewpoint computation library)
 * For more info on the project, contact Roberto Ranon at roberto.ranon@uniud.it.
 *
 * Copyright (c) 2013- University of Udine, Italy - http://hcilab.uniud.it
 * Also see acknowledgements in readme.txt
--------------------------------------------------------------------------------------
 */

/*
 * QuickTesting.cs  
 * Code for quick testing of the library.
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;

public class VCTesting : MonoBehaviour
{
	
	// a transform is used to specify an AABB of allowed camera positions for VC problems
	// the position field specifies the center of the AABB, the scale field specifies the size
	public Transform problemBounds;

	// array of problem targets
	public GameObject[] vc_targets;

	public float solverTime = 20.0f;

	public float randomParticles = 0.0f;

	public int visibilityRays = 6;

	public bool doubleSidedVisibilityChecking = false;

	public bool randomRayCasts = false;

	public bool debugMode = false;



	// camera man
	CLLookAtCameraMan camLibCam;
	VCLookAtProblemDomain cameraDomain;

	// solver
	PSOSolver psoSolver;

	List<GameObject> debugAABB = new List<GameObject> ();
	List<GameObject> viewpoints = new List<GameObject> ();

	void Start ()
	{
		
		// create main library objects: camera man, solver, problem bounds
		camLibCam = new CLLookAtCameraMan ();	
		psoSolver = new PSOSolver (6); // for 6 degrees of freedom VC problem
		psoSolver.SetSolverParameters (20, randomParticles, new float[]{1.0f, 1.0f, 0.8f, 0.4f}); // number of particles, fraction of randomly initialized particles, c1, c2, w_init, w_end
		Camera unityCamera = GetComponentInParent<Camera> ();
		camLibCam.unityCamera = unityCamera;
		
	}


	void InitVCProblem () {
		camLibCam.unityCamera.enabled = true;


		// define VC problem bounds, now taken from problemBounds game object
		cameraDomain = new VCLookAtProblemDomain();
		cameraDomain.positionBounds = new Bounds (problemBounds.position, problemBounds.localScale);
		cameraDomain.lookAtBounds = new Bounds (problemBounds.position, problemBounds.localScale);
		// roll and FOV are fixed in this example
		cameraDomain.rollBounds = new Vector2 (0.0f, 0.0f);
		cameraDomain.yFOVBounds = new Vector2 (camLibCam.unityCamera.fieldOfView, camLibCam.unityCamera.fieldOfView);

		camLibCam.cameraDomain = cameraDomain;

		List<GameObject> gos = new List<GameObject> (vc_targets);

		// build VC problem with selected game object ( weighted sum of properties )
		buildVCProblem (gos, 0.1f, true);
		// update look-at bounds to AABB of targets
		cameraDomain.lookAtBounds = camLibCam.UpdateTargets ();

		if (debugMode) {

			for (int i = debugAABB.Count - 1; i >= 0; i--) {
				DestroyImmediate (debugAABB [i]);
			}
			debugAABB.Clear ();	

			//Debug.Log ("We have " + camLibCam.targets.Count + "targets.");

			foreach (CLTarget t in camLibCam.targets) {

				GameObject box = GameObject.CreatePrimitive (PrimitiveType.Cube);
				box.name = t.gameObject.name + "_AABB";
				box.transform.position = t.boundingBox.center;
				box.transform.localScale = t.boundingBox.size;
				box.GetComponent<MeshRenderer> ().enabled = false;
				box.layer = 2;
				debugAABB.Add (box);

			}

		}

		foreach (GameObject go in viewpoints)
			DestroyImmediate (go);
		viewpoints.Clear ();


	}
	
	void ComputeAndShowCamera ( ) {
		CLViewpoint result = psoSolver.SearchOptimal (solverTime, 0.999f, camLibCam, new List<CLCandidate> (), false, true);
		Debug.Log ("Satisfaction after " + psoSolver.iterations + " iterations: " + result.satisfaction [0] + 
			", best iteration: " + psoSolver.iterOfBest );

		// update camera with found solution
		camLibCam.updateCamera (result.psoRepresentation);
	}


	// this builds a VC problem from a list of targets. For each target, we request size, visibility, vertical
	// and horizontal orientation
	void buildVCProblem (List<GameObject> targetObjects, float desiredSize, bool addChildren)
	{

		// define list of properties
		List<CLVisualProperty> properties = new List<CLVisualProperty> ();
		List<CLVisualProperty> allProperties = new List<CLVisualProperty> ();
		List<CLTarget> targets = new List<CLTarget> ();
		List<float> weights = new List<float> ();

		int layerMask = (1 << 2);

		float preferredSize = desiredSize / (targetObjects.Count);

		// for each game object in the list
		foreach (GameObject targetobj in targetObjects) {

			List<GameObject> renderables = getChildrenWithRenderers (targetobj);
			List<GameObject> colliders = getChildrenWithColliders (targetobj);

			if ((renderables.Count > 0) && (colliders.Count > 0)) {  

				CLTarget target = new CLTarget (targetobj, renderables, colliders, layerMask, CLTarget.VisibilityPointGenerationMethod.UNIFORM_IN_BB, 6);
				targets.Add ( target );

				List<CLTarget> properties_targets = new List<CLTarget> ();
				properties_targets.Add (target);

				//area property with 2.5 weight
				List<float> sizeSatFuncCtrlX = new List<float> { 0.0f, 0.002f, preferredSize, 0.4f, 0.5f, 1.0f };
				List<float> sizeSatFuncCtrlY = new List<float> { 0.0f, 0.1f, 0.8f, 1.0f, 0.1f, 0.0f };
				CLSizeProperty sizeP = new CLSizeProperty (CLSizeProperty.SizeMode.AREA, targetobj.name + " size", properties_targets, sizeSatFuncCtrlX, sizeSatFuncCtrlY);
				weights.Add (2.5f);
				properties.Add (sizeP);

				// orientation property (see from front) with w = 1.0
				List<float> hORFuncCtrlX = new List<float> { -180.0f, -90f, 0.0f, 90f, 180.0f };
				List<float> hORFuncCtrlY = new List<float> { 0.0f, 0.1f, 1.0f, 0.1f, 0.0f };
				CLOrientationProperty orP = new CLOrientationProperty (CLOrientationProperty.OrientationMode.HORIZONTAL, targetobj.name + " orientation",
					properties_targets, hORFuncCtrlX, hORFuncCtrlY);
				properties.Add (orP);
				weights.Add (1.0f);


				// v-orientation property (see from 90 to top), w=1.5
				List<float> vORFuncCtrlX = new List<float> { 0.0f, 90.0f, 95f, 180.0f };
				List<float> vORFuncCtrlY = new List<float> { 0.0f, 1.0f, 0.1f, 0.0f };
				CLOrientationProperty orPV = new CLOrientationProperty (CLOrientationProperty.OrientationMode.VERTICAL_WORLD,
					targetobj.name + " vorientation", properties_targets, vORFuncCtrlX, vORFuncCtrlY);
				properties.Add (orPV);
				weights.Add (1.5f);

				// occlusion property with 4.0 weight 
				List<float> occlFuncCtrlX = new List<float> { 0.0f, 0.5f, 0.6f, 1.0f };
				List<float> occlFuncCtrlY = new List<float> { 1.0f, 0.7f, 0.1f, 0.0f };
				CLOcclusionProperty occlP = new CLOcclusionProperty (targetobj.name + " occlusion", properties_targets, occlFuncCtrlX, occlFuncCtrlY, doubleSidedVisibilityChecking, randomRayCasts);
				weights.Add (4.0f);
				properties.Add (occlP);

			}
		}

		CLTradeOffSatisfaction satFunction = new CLTradeOffSatisfaction("all", targets, properties, weights);
		allProperties.AddRange (properties);
		allProperties.Insert (0, satFunction);

		camLibCam.SetSpecification( allProperties );

	}


	
	void EvaluateCamera() {

		float result = camLibCam.EvaluateSatisfaction(-0.1f);
		foreach ( CLVisualProperty pi in camLibCam.properties )
		{
			Debug.Log (pi.name + " sat:" + pi.satisfaction + " inscreen:" + pi.inScreenRatio);
		}

		Debug.Log ("Targets: " + camLibCam.targets.Count + " sat: " + result );


	}


	void DisplaySolutions ( CLCandidate[] candidates, bool useBestPosition ) {



		for (int i=0; i<psoSolver.numberOfCandidates; i++) {
			GameObject newView = new GameObject();
			newView.AddComponent<Camera>();
			newView.GetComponent<Camera> ().enabled = false;
			viewpoints.Add (newView);
			float[] position = new float[6];

			if (useBestPosition) {
				position = candidates[i].bestPosition;

			} else {  // FASolver
				position = candidates[i].position;
			}

			newView.transform.position = new Vector3 (position[0],position[1],position[2]);
			newView.transform.LookAt( new Vector3(position[3],position[4],position[5]));


		}

	}
	

	void Update ()
	{

		// solves current problem
		if (Input.GetKeyDown ("p")) {
			InitVCProblem ();
			ComputeAndShowCamera ();
			DisplaySolutions (psoSolver.candidates, false);
		}
		
		// evaluates current camera against current problem
		if (Input.GetKeyDown ("e")) {
			InitVCProblem ();
			EvaluateCamera();
		}

		// show visibility points of current targets
		if (Input.GetKeyDown ("l")) {
			InitVCProblem ();

			foreach (Vector3 p in camLibCam.targets[0].visibilityPoints) {

				GameObject newView = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				newView.transform.position = p;
				newView.transform.localScale = new Vector3 (0.1f, 0.1f, 0.1f);
			}

		}

		// test satisfaction n times on random cameras
		if (Input.GetKeyDown ("t")) {

			InitVCProblem ();

			float beginTime = Time.realtimeSinceStartup;

			for (int i = 0; i < 10000; i++) {
				camLibCam.EvaluateSatisfaction (cameraDomain.ComputeRandomViewpoint (6), 0.0001f);
			}

			float endTime = Time.realtimeSinceStartup;

			Debug.Log ("10000 evaluations in " + (endTime - beginTime) + " seconds.");

		}

		// initializes candidates and shows them
		if (Input.GetKeyDown ("i")) {
			InitVCProblem ();
			psoSolver.evaluator = camLibCam;
			psoSolver.InitializeCandidates (new List<CLCandidate> ());
			DisplaySolutions (psoSolver.candidates, false);
		}

		// finds a camera that satisfies a size property
		if (Input.GetKeyDown ("c")) {

			InitVCProblem ();
			float distance = camLibCam.targets [0].ComputeDistanceFromSize (0.5f, CLSizeProperty.SizeMode.HEIGHT, camLibCam, 
				camLibCam.unityCamera.fieldOfView);
			Debug.Log (distance);
			Vector3 pos = camLibCam.targets [0].ComputeWorldPosFromSphericalCoordinates (distance, 0.99F*2*Mathf.PI, Mathf.PI/2);
			camLibCam.unityCamera.transform.position = pos;
			camLibCam.unityCamera.transform.LookAt (camLibCam.targets [0].boundingBox.center);

		}

	}
	
	




	
	
	
	// returns a children game object matching a certain name
	static public GameObject getChildGameObject (GameObject fromGameObject, string withName)
	{
		//Author: Isaac Dart, June-13.
		Transform[] ts = fromGameObject.transform.GetComponentsInChildren<Transform> ();
		foreach (Transform t in ts)
			if (t.gameObject.name == withName)
				return t.gameObject;
		return null;
	}
	
	// returns all game object with colliders associated to a game object or its children
	static public List<GameObject> getChildrenWithColliders (GameObject fromGameObject)
	{
		List<GameObject> result = new List<GameObject> ();
		Collider[] ts = fromGameObject.transform.GetComponentsInChildren<Collider> ();
		foreach (Collider t in ts)
			result.Add (t.gameObject);
		return result;
	}
	
	// returns all game object with enabled mesh renderer associated to a game object or its children
	static public List<GameObject> getChildrenWithRenderers (GameObject fromGameObject)
	{
		List<GameObject> result = new List<GameObject> ();
		Renderer[] ts = fromGameObject.transform.GetComponentsInChildren<Renderer> ();
		foreach (Renderer t in ts)
			if (t.enabled)
				result.Add (t.gameObject);
		return result;
	}
	
	
	
	
	
	
	

	
	
}



