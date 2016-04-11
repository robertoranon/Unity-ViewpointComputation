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

	// camera man
	CLLookAtCameraMan camLibCam;

	// solver
	PSOSolver psoSolver;

	void Start ()
	{
		
		// create main library objects: camera man, solver, problem bounds
		camLibCam = new CLLookAtCameraMan ();	
		psoSolver = new PSOSolver (6); // for 6 degrees of freedom VC problem
		psoSolver.SetSolverParameters (20, 0.3f, new float[]{1.0f, 1.0f, 1.0f, 0.4f}); // number of particles, fraction of randomly initialized particles, c1, c2, w_init, w_end
		Camera unityCamera = GetComponentInParent<Camera> ();
		camLibCam.unityCamera = unityCamera;
		
	}
	
	void ComputeAndShowCamera ( ) {
		
		camLibCam.unityCamera.enabled = true;

	
		// define VC problem bounds, now taken from problemBounds game object
		VCLookAtProblemDomain cameraDomain = new VCLookAtProblemDomain();
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

		CLViewpoint result = psoSolver.SearchOptimal (20.0f, 0.999f, camLibCam, new List<CLCandidate> (), false, true);
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

		float preferredSize = desiredSize / (targetObjects.Count);

		// for each game object in the list
		foreach (GameObject targetobj in targetObjects) {

			List<GameObject> renderables = getChildrenWithRenderers (targetobj);
			List<GameObject> colliders = getChildrenWithColliders (targetobj);

			if ((renderables.Count > 0) && (colliders.Count > 0)) {  

				CLTarget target = new CLTarget (2, targetobj, renderables, colliders , true,6);
				targets.Add ( target );

				List<CLTarget> properties_targets = new List<CLTarget> ();
				properties_targets.Add (target);

				// size property with 2.5 weight
				List<float> sizeSatFuncCtrlX = new List<float> { 0.0f, 0.002f, preferredSize, 0.4f, 0.5f, 1.0f };
				List<float> sizeSatFuncCtrlY = new List<float> { 0.0f, 0.1f, 0.8f, 1.0f, 0.1f, 0.0f };
				CLSizeProperty sizeP = new CLSizeProperty (CLSizeProperty.SizeMode.AREA, targetobj.name + " size", properties_targets, sizeSatFuncCtrlX, sizeSatFuncCtrlY);
				weights.Add (2.5f);
				properties.Add (sizeP);

				// orientation property (see from front) with w = 1.0
				List<float> hORFuncCtrlX = new List<float> { -180.0f, 0.0f, 180.0f };
				List<float> hORFuncCtrlY = new List<float> { 0.0f, 1.0f, 0.0f };
				CLOrientationProperty orP = new CLOrientationProperty (CLOrientationProperty.OrientationMode.HORIZONTAL, targetobj.name + " orientation",
					properties_targets, hORFuncCtrlX, hORFuncCtrlY);
				properties.Add (orP);
				weights.Add (1.0f);


				// v-orientation property (see from 90 to top), w=1.5
				List<float> vORFuncCtrlX = new List<float> { 0.0f, 90.0f, 180.0f };
				List<float> vORFuncCtrlY = new List<float> { 0.0f, 1.0f, 0.0f };
				CLOrientationProperty orPV = new CLOrientationProperty (CLOrientationProperty.OrientationMode.VERTICAL_WORLD,
					targetobj.name + " vorientation", properties_targets, vORFuncCtrlX, vORFuncCtrlY);
				properties.Add (orPV);
				weights.Add (1.5f);

				// occlusion property with 4.0 weight 
				List<float> occlFuncCtrlX = new List<float> { 0.0f, 0.5f, 0.6f, 1.0f };
				List<float> occlFuncCtrlY = new List<float> { 1.0f, 0.7f, 0.1f, 0.0f };
				CLOcclusionProperty occlP = new CLOcclusionProperty (targetobj.name + " occlusion", properties_targets, occlFuncCtrlX, occlFuncCtrlY, false);
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
	

	void Update ()
	{
		if (Input.GetKeyDown ("p")) {
			ComputeAndShowCamera ();
		}
		
		if (Input.GetKeyDown ("e")) {
			EvaluateCamera();
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



