/*
-----------------------------------------------------------------------------
This source file is part of ViewpointComputationLib (a viewpoint computation library)
For more info on the project, contact Roberto Ranon at roberto.ranon@uniud.it.

Copyright (c) 2013- University of Udine, Italy - http://hcilab.uniud.it
-----------------------------------------------------------------------------

 CLSolver.cs: file defining classes to solve a VC problem

-----------------------------------------------------------------------------
*/

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


/// <summary>
/// Candidate. A particle in the PSO solver, or a firefly in the Firefly solver
/// </summary>
public class CLCandidate : IComparable<CLCandidate>
{
		
	/// <summary>
	/// Dimension of the candidate.
	/// </summary>
	public int dimension { get; set; }
		
	/// <summary>
	/// Position of the candidate.
	/// </summary>
	public float[] position { get; set; }
        
	/// <summary>
	/// Velocity of the candidate.
	/// </summary>
	public float[] velocity { get; set; }
        
	//// <summary>
	/// Best position reached so far by the candidate
	/// </summary>
	public float[] bestPosition { get; set; }
        
	//// <summary>
	/// Candidate best evaluation so far
	/// </summary>
	public float bestEvaluation;
        
	//// <summary>
	/// Candidate last evaluation 
	/// </summary>
	public float evaluation;
        
	//// <summary>
	/// True if candidate position inside search space 
	/// </summary>
	public bool inSearchSpace;

	//// <summary>
	/// Times the candidate was out of search space 
	/// </summary>
	public int timesOutOfSearchSpace;
        
	//// <summary>
	/// index of leader Candidate
	/// </summary>
	public int leader;

	public int bestIteration;
	
		
	/// <summary>
	/// Initializes a new instance of the <see cref="Candidate"/> class.
	/// </summary>
	/// <param name='_dimension'>
	/// dimensions of the candidate
	/// </param>
	public CLCandidate (int _dimension)
	{
		dimension = _dimension;
		bestEvaluation = 0;
		position = new float[dimension];
		velocity = new float[dimension];
		bestPosition = new float[dimension];
	}

	//// <summary>
	/// Returns objective function value for the candidate, or -2 if candidate not in search space
	/// </summary>
	public float EvaluateSatisfaction (CLCameraMan evaluator, bool lazy)
	{
		// if we are not in search space there is no point evaluating the objective function
		if (!InSearchSpace (evaluator)) {
			timesOutOfSearchSpace++;
			return -2.0f;  // give penalty to out-of-bounds candidates
		}
		inSearchSpace = true;
		if (lazy) {
			evaluation = evaluator.EvaluateSatisfaction (position, bestEvaluation);
		}
		else
			evaluation = evaluator.EvaluateSatisfaction (position, -0.001f);

		return evaluation;	 
	}

	/// <summary>
	/// Check if candidate is in problem search space
	/// </summary>
	/// <returns><c>true</c>, if search space was ined, <c>false</c> otherwise.</returns>
	/// <param name="evaluator">CLCameraMan evaluator</param>
	/// <param name="checkGeometry">If set to <c>true</c> check geometry.</param>
	public bool InSearchSpace (CLCameraMan evaluator)
	{
		return evaluator.InSearchSpace (position);		
	}

	public int CompareTo(CLCandidate other)
	{
		// allow auto sort high sat to low sat
		if (this.evaluation > other.evaluation)
			return -1;
		else if (this.evaluation < other.evaluation)
			return +1;
		else
			return 0;
	}

	public float Distance( CLCandidate other, bool useBestPosition)
	{
		
		float ssd = 0.0f; // sum squared differences (Euclidean)
		float[] position1, position2;

		if (useBestPosition) {
			position1 = bestPosition;
			position2 = other.bestPosition;
		} else {
			position1 = position;
			position2 = other.position;
		}

		for (int i = 0; i < dimension; i++)
			ssd += (position1[i] - position2[i]) * (position1[i] - position2[i]);
		return Mathf.Sqrt(ssd);
	}
		
}

/// <summary>
/// Abstract class representing a solver of VC problems.
/// </summary>
public abstract class CLSolver {

	/** dimensionality of a candidate, (e.g. 8 for position, look-at point, roll, fov; 6 for position, look-at point, etc ) */
	protected int candidateDimension;

	/** array of candidates used by the solver, up to 300 */
	public CLCandidate[] candidates = new CLCandidate[300];

	/** Number of candidates actually used by the search */
	public int numberOfCandidates;

	/** Camera used to evaluate a candidate satisfaction */
	public CLCameraMan evaluator;

	/** fraction of randomly initialized candidates (the other are initialized smartly depending on the problem) */
	protected float randomPart=0.3f;
	
	/** max sat reachable (normalized) */
	protected float maxSatisfaction;

	/** exit condition code (0 = time elapsed, 1 = accuracy reached, 2 = continue execution) */
	public int exitCondition;

	/** internal random generator */
	protected System.Random rnd;

	/** current solver iteration */
	public int iterations;
	
	/** best iteration so far */
	public int iterOfBest;

	/** max time allowed for search, in seconds */
	protected float timeLimit;

	/** begin time of search, in seconds */
	protected float beginTime;

	/** time elapsed since beginning of search, in seconds */
	protected float elapsedTime;

	/** list of best viewpoints found by search, last is usually the best */
	public List<CLViewpoint> globalBestViewpoints;

	/** min value of search space */
	public float[] minSearchRange;

	/** max value of search space */
	public float[] maxSearchRange;

	/** max value of search space */
	public float[] searchRanges;

	/** average range of search space */
	public float averageRange;

	/** constructor */
	public CLSolver ( int _candidateDimension ) {

		candidateDimension = _candidateDimension;

		// we init all candidates once and for all
		for (int i = 0; i<candidates.Length; i++) {
			candidates[i] = new CLCandidate( candidateDimension );
		}
	}

	//// <summary>
	/// Returns the best found CLViewpoint given the allowed time in milliseconds, 
	/// the required satisfaction threshold in [0,1], a CLCameraMan for evaluating a 
	/// candidate satisfaction. If init is true, we start search from scratch, i.e. by
	/// first initializing candidates; otherwise, we use the current candidates
	/// </summary>
	public CLViewpoint SearchOptimal (float _timeLimit, float _satisfactionThreshold, CLCameraMan _evaluator, List<CLCandidate> initialCandidates, bool checkGeometry=false, bool init=true) {

		//if init, initialize n candidates ( with r_part candidates randomly initialized )
		beginTime = Time.realtimeSinceStartup;
		elapsedTime = 0.0f;
		timeLimit = _timeLimit / 1000.0f;
		
		if (init) {			
			rnd = new System.Random ();
			iterations = 0;
			iterOfBest = 0;
			this.evaluator = _evaluator;
			this.maxSatisfaction = _satisfactionThreshold;
			globalBestViewpoints = new List<CLViewpoint> ();
			minSearchRange = evaluator.GetMinCameraParameters(candidateDimension);
			maxSearchRange = evaluator.GetMaxCameraParameters(candidateDimension);
			searchRanges = evaluator.GetParametersRange(candidateDimension);
			averageRange = 0.0f;
			for (int i=0; i<candidateDimension; i++) {
				averageRange += searchRanges[i];
			}
			averageRange = averageRange / candidateDimension;
			InitializeCandidates (initialCandidates);
			InitSolverParameters (_timeLimit);
			elapsedTime = Time.realtimeSinceStartup - beginTime;
		}
	
		if (elapsedTime > timeLimit)
			exitCondition = 0;
		else
			exitCondition = 2;

		while (DoAnotherIteration()) {
			
			iterations++; // we start from 1

			//  execute loop body (dependent on method).
			exitCondition = ExecuteSearchIteration ();

		}

		//elaborate results and return best solution (access to other solutions should be provided)

		// this is for handling the case where no optima have been found at all (e.g. not enough time)
		if (globalBestViewpoints.Count == 0) {
			
			CLViewpoint noSolution = new CLViewpoint();
			noSolution.satisfaction = new List<float>();
			noSolution.properties = new List<CLVisualProperty> (evaluator.properties);
			foreach ( CLVisualProperty p in noSolution.properties ) {
				noSolution.satisfaction.Add ( -1.0f );
			}
			noSolution.psoRepresentation = new float[]{0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 60.0f};
			globalBestViewpoints.Add ( noSolution);
			
		}

		// this returns only one solution !!!!
		return globalBestViewpoints[globalBestViewpoints.Count-1];


	}

	/** Sets solver parameter; the first two are the number of candidates and the percentage of randomly
	 initialized candidates, the others are subclass-specific */
	public virtual void SetSolverParameters( int _nCandidates, float r_part, float[] otherParams ) {
		numberOfCandidates = _nCandidates;
		randomPart = r_part;
	}
	
	/** Updates solver parameters, e.g. after a search iteration */
	protected abstract void UpdateSolverParameters (float elapsedTime);
	
	/** Inits solver parameters */
	protected abstract void InitSolverParameters (float timeLimit);

	/** Inits solver candidates */
	public abstract void InitializeCandidates (List<CLCandidate> initialCandidates);

	/** Executes a search iteration - to be implemented in subclasses */
	protected abstract int ExecuteSearchIteration ();
	
	/** Returns true of search has to continue after an iteration. Actually tests max number of
         iterations and maximum elapsed time.
         */
	protected bool DoAnotherIteration ()
	{
		return ((iterations < 3000) && (exitCondition==2));
	}

	/** Performs candidates clustering */
	public HierarchicalClustering PerformCandidateClustering (float minSat, bool useBestPosition) {

		return new HierarchicalClustering (this, minSat, useBestPosition);

	}


	public void ComputeNRandomCandidates (int startingCandidate, int numberOfCandidates)
	{
		// if r_part = 1.0 : fully random initialization
		bool smart = false;
		if (randomPart < 0.99f)
			smart = true;

		int currentCandidate;

		// init numberOfCandidates * r_part random candidates
		for (currentCandidate = startingCandidate; currentCandidate < numberOfCandidates * randomPart; currentCandidate++) {
			candidates [currentCandidate].position = evaluator.ComputeRandomViewpoint (candidates[0].dimension,false);
		}

		if (smart) {

			// now, if we have just one target, all remaining candidates are initialized according to its properties. Otherwise,
			// we split the remaining candidates into two parts: one, to be initialized per target (70%), and a second part,
			// to be initialized per allTargets (30%)
			int candidatesToBeInitializedPerTarget, candidatesToBeInitializedGlobally;
			//if (targets.Count == 1) {

			candidatesToBeInitializedPerTarget = numberOfCandidates - (currentCandidate - startingCandidate);
			candidatesToBeInitializedGlobally = 0;

			//} else {
			//	candidatesToBeInitializedPerTarget = (int)Math.Ceiling ((numberOfCandidates - (currentCandidate - startingCandidate)) * 0.7f);
			//	candidatesToBeInitializedGlobally = (int)Math.Floor ((numberOfCandidates - (currentCandidate - startingCandidate)) * 0.3f);
			//}

			float partitionFactor = candidatesToBeInitializedPerTarget / evaluator.targets.Count;

			foreach (CLTarget t in evaluator.targets) {
				// init partitionFactor * t.contribution particles according to target
				for (int i = 0; i < partitionFactor; i++) {
					candidates [currentCandidate].position = evaluator.ComputeRandomViewpoint (candidates[0].dimension,smart, t);
					currentCandidate++;

				}

			}

			// remaining candidates to be initialized globally
			//for (int i = 0; i < candidatesToBeInitializedGlobally; i++) {
			//	candidates [currentCandidate].position = ComputeRandomCandidate (smart, null);
			//	currentCandidate++;
			//
			//}
		}
	}

}


/// <summary>
/// PSO solver for VC problems
/// </summary>
public class PSOSolver : CLSolver
{
				
	
	public PSOSolver ( int _candidateDimension ) : base ( _candidateDimension ) {
	}

		
	/** Updates solver parameters, e.g. after a search iteration */
	protected override void UpdateSolverParameters (float elapsedTime)
	{
			
		if (elapsedTime <= weightIterations) {
			w = maxInertiaWeight - elapsedTime * weightDecrement; //linear
		}

		//w = 0.4f;
			
	}

	
	//// <summary>
	/// Returns the best found Candidate given the allowed time in milliseconds, 
	/// the required satisfaction threshold in [0,1], and a SmartCamera defining the 
	/// problem with its properties and targets
	/// </summary>
	protected override int ExecuteSearchIteration ()	{

		UpdateSolverParameters (elapsedTime);
		steadyParticles = true; // we hypothesize particles are steady at the beginning of each iteration
        
		for (int currentCandidate=0; currentCandidate<numberOfCandidates; currentCandidate++) {
            
				// update current candidate, from second iteration
				if (iterations != 1)
					UpdateCandidate (currentCandidate);
				else
					steadyParticles = false;
            
				// evaluate current candidate
				candidates[currentCandidate].EvaluateSatisfaction (evaluator, true);

				// update leaders
				UpdateLeaders (currentCandidate, candidates[currentCandidate].evaluation);
            
				if (candidates[currentCandidate].evaluation >= maxSatisfaction) {
					// we have reached the required satisfaction threshold,
					goto EXIT;
				}
            
				// check time elapsed condition
				elapsedTime = Time.realtimeSinceStartup - beginTime;
            
				if (elapsedTime >= timeLimit) {
					// we have used all the time at disposal
					goto EXIT;
                
				}
            
			} // end iteration
        

		EXIT:

		if (elapsedTime >= timeLimit)
			return 0;
		else if (candidates [bestCandidate].bestEvaluation >= maxSatisfaction)
			return 1;
		else
			return 2;
					
	}
        
        
	//// <summary>
	/// Sets PSO solver parameters
	/// </summary>
	public override void SetSolverParameters (int _nCandidates, float r_part, float[] otherParams )  
	{
		base.SetSolverParameters ( _nCandidates, r_part, otherParams );
		this.c1 = otherParams[0];
		this.c2 = otherParams[1];
		this.maxInertiaWeight = otherParams[2];
		this.minInertiaWeight = otherParams[3];
		
	}
          
	///// <summary>
	/// Updates i-th candidate description
	/// </summary>
	private void UpdateCandidate (int i)
	{
		//float random_w = (float)rnd.NextDouble () / 2 + 0.5f;

		bool tinyVelocity = true; // we hypothesize particle velocity is tiny
		for (int j = 0; j < candidates[i].dimension; j++) {
			candidates [i].velocity [j] = w * candidates [i].velocity [j] + c1 * (float)rnd.NextDouble() *
				(candidates [i].bestPosition [j] - candidates [i].position [j]) + c2 * (float)rnd.NextDouble() *
				(candidates [bestCandidate].bestPosition [j] - candidates [i].position [j]);

			if ( candidates[i].velocity[j] > maxSearchRange[j] )
				candidates[i].velocity[j] = maxSearchRange[j];
			if ( candidates[i].velocity[j] < -maxSearchRange[j] )
				candidates[i].velocity[j] = -maxSearchRange[j];

			if ( candidates[i].velocity[j] > 0.001f * maxSearchRange[j] )
				tinyVelocity = false; // as soon as the particle has no tiny velocity in one dimension, the particle has no tiny velocity

			candidates [i].position [j] = candidates [i].velocity [j] + candidates [i].position [j];
		}	
		if (!tinyVelocity) // if the particle has no tiny velocity, the swarm is not in a steady state
			steadyParticles = false;
	}
		

        
        
	/** updates a candidate leaders
         @param i index of the candidate whose leaders are updated
         @param evaluation evaluation of the current candidate */
	private void UpdateLeaders (int i, float evaluation)
	{
		if (evaluation > candidates [i].bestEvaluation) {
        
			// new local optimum
			for (int j=0; j<candidates[i].dimension; j++)
				candidates [i].bestPosition [j] = candidates [i].position [j];
        
			candidates [i].bestEvaluation = evaluation;
        
			if ((evaluation > candidates [bestCandidate].bestEvaluation) || ((i == bestCandidate) && (evaluation >= candidates [bestCandidate].bestEvaluation))) {
				// new global optimum
				bestCandidate = i;
				iterOfBest = iterations;
				//Debug.Log ( "New global optimum with sat: " + evaluation + " and position" + 
				//new Vector3(candidates[i].bestPosition[0], candidates[i].bestPosition[1], candidates[i].bestPosition[2] ).ToString("F5"));
				StoreNewGlobalLeader (i);
            
			}
		}
    
		candidates [i].leader = bestCandidate;
			
	}
        
        
	/** Initializes specific solver parameters */
	protected override void InitSolverParameters (float maxTime)
	{
		maxSearchRange = this.evaluator.GetParametersRange (candidateDimension);
		w = maxInertiaWeight; // initally w is set to the max inertia value	
		// w decrement based on time
		weightIterations = 0.85f * maxTime;
		weightDecrement = (maxInertiaWeight - minInertiaWeight) / weightIterations;
		steadyParticles = false;
		bestCandidate = 0;
	}
        
       
	/** Initializes the candidates
         @remark does not need to compute each candidate satisfaction, but needs to set each
         candidate bestEvaluation to 0 and bestPosition to current position
         @note this implments fully random initialization inside search space, with zero velocity */
	public override void InitializeCandidates (List<CLCandidate> initialCandidates)
	{

		int k = 0;
		foreach (CLCandidate c in initialCandidates) 
		{
			candidates[k].position = c.position;
			k++;
		}

		ComputeNRandomCandidates (k, numberOfCandidates - initialCandidates.Count);
        
		for (int i=0; i<numberOfCandidates; i++) {  
			for (int j=0; j<candidates[i].dimension; j++) {
                
				candidates [i].bestPosition [j] = candidates [i].position [j];
				candidates [i].velocity [j] = 0.0f;
			}
			candidates [i].bestEvaluation = -1.0f;
			candidates [i].inSearchSpace = true;
			candidates [i].leader = 0;
			candidates [i].timesOutOfSearchSpace = 0;
            
		}

		maxSearchRange = this.evaluator.GetParametersRange (candidateDimension);
	}
	

	protected void StoreNewGlobalLeader (int leader)
	{
		CLViewpoint newLeaderViewpoint = new CLViewpoint ();
		CLCandidate leaderCandidate = new CLCandidate (candidateDimension);
		
		leaderCandidate.bestEvaluation = candidates [leader].bestEvaluation;
		leaderCandidate.bestPosition = candidates [leader].bestPosition;
		leaderCandidate.evaluation = candidates [leader].evaluation;
		leaderCandidate.inSearchSpace = candidates [leader].inSearchSpace;
		leaderCandidate.leader = candidates [leader].leader;	
		
		newLeaderViewpoint.psoRepresentation = leaderCandidate.bestPosition;
		newLeaderViewpoint.properties = new List<CLVisualProperty> (evaluator.properties);
		newLeaderViewpoint.satisfaction = new List<float> (evaluator.properties.Count);
		newLeaderViewpoint.inScreenRatio = new List<float> (evaluator.properties.Count);
		foreach (CLVisualProperty p in newLeaderViewpoint.properties) {
			newLeaderViewpoint.satisfaction.Add (p.satisfaction);
			newLeaderViewpoint.inScreenRatio.Add (p.inScreenRatio);
		}
		
		
		globalBestViewpoints.Add (newLeaderViewpoint);
		
	}     
        
	
		
	/** PSO cognitive parameter */
	private float c1;
                
	/** PSO social parameter */
	private float c2;
        
	/** Initial value of inertia weight */
	public float maxInertiaWeight;
              
	/** Final value of inertia weight */
	private float minInertiaWeight;
        
	/** Number of iterations for which we decrease the inertia weight */
	private float weightIterations;
        
	/** Inertia weight decrement per iteration */
	private float weightDecrement;
          
	/** current inertia weight */
	public float w;

	/** true if particles are steady */
	public bool steadyParticles;
	
	/** index of the best candidate */
	private int bestCandidate;


	
	
}







