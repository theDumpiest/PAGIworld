using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System;
using System.IO;
using System.Linq;

public abstract class sensor
{
	public Rigidbody2D anchorBody = new Rigidbody2D();
    //public Collider2D collider = new Collider2D(); // so we don't look at oursel
	public Vector2 relativePosition = new Vector2();
	public abstract void updateSensor();
	
	protected worldObject getObjectAt(Vector2 pos) 
	{ 
		//close hand, grip whatever
		//find all objects it might possibly grip
		
		//int layerNum = 8; //Normal objects layer
		List<int> validLayers = new List<int>();
		validLayers.Add (LayerMask.NameToLayer("Default"));
		validLayers.Add (LayerMask.NameToLayer("Normal Objects"));
		validLayers.Add (LayerMask.NameToLayer("VisibleButNonreactive"));
		//int[] validLayers = new int[]{0,8};

        /** New way of getting overlapped points using LINQ
         * Find all colliders at the given point that are on the prescribed
         * layers. Get the worldObjects from their parent gameObjects and
         * return the whole lot as a list
         */
        worldObject[] collidedGameObjects = Physics2D.OverlapPointAll(pos)
            .Select(c => c.gameObject)
            .Where(g => validLayers.Contains(g.layer))
            .Select(g => g.GetComponent<worldObject>()).ToArray();

        // If we have one to return, return the first one.
        // The first one will be the closest due to how 
        // OverlapPointAll works
        if (collidedGameObjects.Length > 0)
            return collidedGameObjects[0];

        else
        {
            //create background object to return
            worldObject BG = new worldObject();
            BG.objectName = "Background";
            return BG;
        }
	}

	/// <summary>
	/// returns the absolute coordinates of this sensor
	/// </summary>
	/// <returns>The position.</returns>
	public Vector2 getPosition()
	{
		return anchorBody.GetRelativePoint(relativePosition);
	}
}

public class touchSensor : sensor
{
	public float temp = 0f;
	public float[] texture = new float[3];
	public worldObject objectTouched = new worldObject();
	public float endorphins = 0f;
	
	public touchSensor(Rigidbody2D anchrBdy, Vector2 relPos)
	{
		anchorBody = anchrBdy;
		relativePosition = relPos;
	}
	
	public override void updateSensor()
	{
		Vector2 pos = getPosition();
		objectTouched = getObjectAt(pos);
		temp = objectTouched.temperature;
		if (objectTouched.GetType()==typeof(rewardOrPunishmentController))
			endorphins = ((rewardOrPunishmentController)objectTouched).endorphins;
		else
			endorphins = 0f;
		texture = new float[objectTouched.texture.Length];
		for (int i=0; i<texture.Length; i++)
			texture[i] = objectTouched.texture[i]; 
	}
	
	/// <summary>
	/// Returns a string with all of the values to be returned (see documentation).
	/// Make sure to update the sensor before using this.
	/// </summary>
	/// <returns>The report.</returns>
	public string getReport()
	{
		if (objectTouched.objectName == "Background")
		{
			return "0,0,0,0,0,0,0,Background\n";
		}
		else {
			string toReturn = "1,";
			toReturn += temp.ToString();
			for (int i=0; i<texture.Length; i++)
				toReturn += "," + texture[i].ToString();
			//Debug.Log(texture.Length + " of " + sensor.objectTouched.name);
			toReturn += "," + endorphins.ToString();
			toReturn += "," + objectTouched.objectName;
			return toReturn;
		}
	}
}

public class visualSensor : sensor
{
	public float[] vq = new float[4];
	/// <summary>
	/// the type/category of the object this sensor is sensing, or background
	/// </summary>
	public string type = "";
	/// <summary>
	/// the name/unique id of the object this sensor is sensing, or background
	/// </summary>
	public string name = "";
	public int indexX;
	public int indexY;
	
	public visualSensor(Rigidbody2D anchrBdy, Vector2 relPos, int indexX, int indexY)
	{
		anchorBody=anchrBdy;
		relativePosition=relPos;
		this.indexX = indexX;
		this.indexY = indexY;
	}
	
	public override void updateSensor()
	{
		Vector2 pos = getPosition();
		worldObject obj = getObjectAt(pos);
		type = obj.objectType;
		name = obj.objectName;
		for (int i=0; i<vq.Length; i++)
		{
			if (i>=obj.visualFeatures.Length)
				vq[i]=0;
			else
				vq[i] = obj.visualFeatures[i];
		}
	}
}

public class bodyController : worldObject {
    public ThreadSafeList<JSONAIMessage> messageQueueJ = GlobalVariables.messageQueueJ;
	public ThreadSafeList<string> outgoingMessages = GlobalVariables.outgoingMessages;
	
	//sensors
	public touchSensor[] leftHandSensor = new touchSensor[5];
	public touchSensor[] rightHandSensor = new touchSensor[5];
	public touchSensor[] bodySensor = new touchSensor[8];
	public Vector2[] proprioceptionSensor = new Vector2[2];
	static int numVisualSensorsX = 31, numVisualSensorsY = 21;
	public visualSensor[,] visualSensors = new visualSensor[numVisualSensorsX, numVisualSensorsY];
	static int numPeripheralSensorsX = 16, numPeripheralSensorsY = 11;
	public visualSensor[,] peripheralSensors = new visualSensor[numPeripheralSensorsX, numPeripheralSensorsY];
	//Dictionary<string,sensor> sensorNameLookup = new Dictionary<string, sensor>();
	
	public GameObject[] leftArm;
	public GameObject[] rightArm;
	public worldObject leftHand;
	public worldObject rightHand;
	Rigidbody2D leftHandRigidBody;
	Rigidbody2D rightHandRigidBody;
	
	public ObjectMenu objectMenu; //used for creating objects
	
	//temp: remove this later
	//public Rigidbody2D garf;
	
	//stuff for picking up things with hands
	public bool[] handIsClosed = new bool[]{false, false}; 
	//DistanceJoint2D leftJoint = null, rightJoint = null; 
	DistanceJoint2D[] handJoint = new DistanceJoint2D[]{null, null}; //this will connect the right hand to whatever it picked up
	public bool powerMode = false; //true if the hands move with 10x normal force
	
	// Use this for initialization
	void Start () {
		Application.runInBackground = true;
		
		leftHandRigidBody = leftHand.GetComponent<Rigidbody2D>();
		rightHandRigidBody = rightHand.GetComponent<Rigidbody2D>();
		leftHand.objectName = "leftHand";
		rightHand.objectName = "rightHand";
		
		//initialize sensors
		leftHandSensor[0] = new touchSensor(leftHandRigidBody, new Vector2(0, .65f));
		leftHandSensor[1] = new touchSensor(leftHandRigidBody, new Vector2(0.6f, 0));
		leftHandSensor[2] = new touchSensor(leftHandRigidBody, new Vector2(0, -.65f));
		leftHandSensor[3] = new touchSensor(leftHandRigidBody, new Vector2(-0.6f, 0));
		leftHandSensor[4] = new touchSensor(leftHandRigidBody, new Vector2(0, 0));
		rightHandSensor[0] = new touchSensor(rightHandRigidBody, new Vector2(0, .65f));
		rightHandSensor[1] = new touchSensor(rightHandRigidBody, new Vector2(0.6f, 0));
		rightHandSensor[2] = new touchSensor(rightHandRigidBody, new Vector2(0, -.65f));
		rightHandSensor[3] = new touchSensor(rightHandRigidBody, new Vector2(-0.6f, 0));
		rightHandSensor[4] = new touchSensor(rightHandRigidBody, new Vector2(0, 0));
		bodySensor[0] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(0, 1.2f));
		bodySensor[1] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(0.9f, 0.9f));
		bodySensor[2] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(1.2f, 0));
		bodySensor[3] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(0.9f, -0.9f));
		bodySensor[4] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(0, -1.2f));
		bodySensor[5] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(-0.9f, -0.9f));
		bodySensor[6] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(-1.2f, 0));
		bodySensor[7] = new touchSensor(GetComponent<Rigidbody2D>(), new Vector2(-0.9f, 0.9f));
		Vector2 blCorner = new Vector2(-2.25f,1.65f); //the distance the bottom left corner of the visual field is from the body's center
        for (int x = 0; x < numVisualSensorsX; x++)
        {
            for (int y = 0; y < numVisualSensorsY; y++)
            {
                visualSensors[x, y] = new visualSensor(GetComponent<Rigidbody2D>(), blCorner + new Vector2(x * 0.148f, y * 0.1475f), x, y);
            }
        }
        
		blCorner = new Vector2(-14.5f,0.5f); //the distance the bottom left corner of the peripheral field is from the body's center
        for (int x = 0; x < numPeripheralSensorsX; x++)
        {
            for (int y = 0; y < numPeripheralSensorsY; y++)
            {
                peripheralSensors[x, y] = new visualSensor(GetComponent<Rigidbody2D>(), blCorner + new Vector2(x * 2f, y * 2f), x, y);
            }
        }
        

		/*//test:
		Name r = new Name("r");
		r.addCondition("BPx", '<', 0.5f);
		
		GlobalVariables.activeReflexes.Add(r);*/ 
		//texture = new Texture2D(1, 1);
		//texture.SetPixel(0,0, Color.blue);
		//texture.Apply();
		string[] s = System.Environment.GetCommandLineArgs();
		foreach (string S in s)
			Debug.Log("cmd: " + S);
	}

	//Texture2D texture;
	void OnGUI()
	{
		
		//GUI.skin.box.normal.background = texture;
		if (GlobalVariables.showPeripheralVisionMarkers)
		{
			Color oldColor = GUI.color;
			GUI.color = Color.red;

            // New way, single foreach loop will go over
            // every entry in 2d array
            foreach (visualSensor p in peripheralSensors)
            {
                Vector2 v = p.getPosition();
                Vector3 v3 = Camera.main.WorldToScreenPoint(new Vector3(v.x, v.y, 0));
                GUI.Label(new Rect(v3.x - 3, (Screen.height - v3.y) - 5, 10, 10), "*");
            }
			GUI.color = oldColor;
		}
		if (GlobalVariables.showDetailedVisionMarkers)
		{

            foreach (visualSensor p in visualSensors)
            {
                Vector2 v = p.getPosition();
                Vector3 v3 = Camera.main.WorldToScreenPoint(new Vector3(v.x, v.y, 0));
                GUI.Label(new Rect(v3.x - 3, (Screen.height - v3.y) - 5, 10, 10), "*");
            }
		}
	}
	
	/// <summary>
	/// interprets the sensor aspect code and checks the current value of the sensor
	/// referred to by it.
	/// </summary>
	/// <returns>The sensor aspect value code.</returns>
	/// <param name="sensorAspectCode">Sensor aspect code.</param>
	float getSensorAspectValue(string sensorAspectCode)
	{
        // For some reason this comes with some white space, so trim it first
        string trimmedCode = sensorAspectCode.Trim();
		float sensorVal=0.0f;
		bool isStandardSensor = false;
		switch (trimmedCode)
		{
			case "Sx":
			sensorVal = GetComponent<Rigidbody2D>().velocity.x;
			break;
			case "Sy":
			sensorVal = GetComponent<Rigidbody2D>().velocity.y;
			break;
			case "BPx":
			sensorVal = GetComponent<Rigidbody2D>().position.x;
			break;
			case "BPy":
			sensorVal = GetComponent<Rigidbody2D>().position.y;
			break;
			case "LPx":
			leftHandSensor[4].updateSensor(); //recall sensor 4 is right in the middle of the hand
			Vector2 relativePoint = GetComponent<Rigidbody2D>().GetPoint(leftHandSensor[4].getPosition());
			sensorVal = relativePoint.x;
			break;
			case "LPy":
			leftHandSensor[4].updateSensor(); //recall sensor 4 is right in the middle of the hand
			relativePoint = GetComponent<Rigidbody2D>().GetPoint(leftHandSensor[4].getPosition());
			sensorVal = relativePoint.y;
			break;
			case "RPx":
			rightHandSensor[4].updateSensor(); //recall sensor 4 is right in the middle of the hand
			relativePoint = GetComponent<Rigidbody2D>().GetPoint(rightHandSensor[4].getPosition());
			sensorVal = relativePoint.x;
			break;
			case "RPy":
			rightHandSensor[4].updateSensor(); //recall sensor 4 is right in the middle of the hand
			relativePoint = GetComponent<Rigidbody2D>().GetPoint(rightHandSensor[4].getPosition());
			sensorVal = relativePoint.y;
			break;
			case "A":
			sensorVal = GetComponent<Rigidbody2D>().rotation;
			break;
			default:
			isStandardSensor = true;
			break;
		}
		
		if (isStandardSensor)
		{//find out which sensor it's polling: left/right hand, body, or visual?
			string[] ss = sensorAspectCode.Split(new char[]{'_'});
			touchSensor ts = null;
			bool isTactileSensor = false;
			if (ss[0].StartsWith("L"))
			{//tactile sensor for left hand
				isTactileSensor = true;
				ts = leftHandSensor[int.Parse(ss[0].Substring(1))];
			}
			if (ss[0].StartsWith("R"))
			{//tactile sensor for right hand
				isTactileSensor = true;
				ts = rightHandSensor[int.Parse(ss[0].Substring(1))];
			}
			if (ss[0].StartsWith("B"))
			{//tactile sensor for body
				isTactileSensor = true;
				ts = bodySensor[int.Parse(ss[0].Substring(1))];
			}
			if (isTactileSensor)
			{ //WHO'S READY FOR ANOTHER MAGICAL ADVENTURE
				switch(ss[1])
				{
				case "tmp":
					ts.updateSensor();
					sensorVal = ts.temp;
					break;
				case "tx1":
					ts.updateSensor();
					sensorVal = ts.texture[0];
					break;
				case "tx2":
					ts.updateSensor();
					sensorVal = ts.texture[1];
					break;
				case "tx3":
					ts.updateSensor();
					sensorVal = ts.texture[2];
					break;
				case "tx4":
					ts.updateSensor();
					sensorVal = ts.texture[3];
					break;
				case "e":
					ts.updateSensor();
					sensorVal = ts.endorphins;
					break;
				}
			}
			else
			{//it's a detailed visual or peripheral sensor
				//ss[0] is e.g. V0.0 and ss[1] is e.g. vq2
				string[] crds = ss[0].Substring(1).Split(new char[]{'.'});
				//Debug.Log(sensorAspectCode);
				//Debug.Log(ss[0]);
				//Debug.Log(ss[1]);
				int x=int.Parse(crds[0]),y=int.Parse(crds[1]);
				visualSensor vs = null;
				if (ss[0][0] == 'V')
					vs = visualSensors[x,y];
				else if (ss[0][0]=='P')
					vs = peripheralSensors[x,y];
				else
					throw new Exception("I don't know this sensor aspect code: " + sensorAspectCode);
				vs.updateSensor();
				switch(ss[1])
				{
				case "vq1":
					sensorVal = vs.vq[0];
					break;
				case "vq2":
					sensorVal = vs.vq[1];
					break;
				case "vq3":
					sensorVal = vs.vq[2];
					break;
				case "vq4":
					sensorVal = vs.vq[3];
					break;
				}
			}
		}
		return sensorVal;
	}
	
	/// <summary>
	/// checks all active reflexes to see if any should be fired
	/// </summary>
	void checkReflexes()
	{	
		List<ReflexJ> activeReflexes_copy = GlobalVariables.activeReflexes.getCopy();
		List<State> activeStates_copy = GlobalVariables.activeStates.getCopy();
		foreach (ReflexJ r in activeReflexes_copy)
		{
			//are the conditions of r all satisfied?
			bool allSatisfied = true;
			foreach (ReflexJ.ConditionJ c in r.Conditions)
			{
				//is c satisfied?
				if (c is ReflexJ.StateConditionJ)
				{
					ReflexJ.StateConditionJ C = (ReflexJ.StateConditionJ)c;
					//check if C.stateName is an active state TODO: make this faster
					bool foundState = false;
					foreach (State st in GlobalVariables.activeStates.getCopy())
					{
						if (st.stateName == C.StateName)
						{
							foundState = true;
							break;
						}
					}
					if (foundState == C.Negated)
					{
						allSatisfied = false;
						break;
					}
				}
				else if (c is ReflexJ.SensoryConditionJ)
				{
					float tolerance = 0.01f;
					ReflexJ.SensoryConditionJ C = (ReflexJ.SensoryConditionJ)c;
					//which sensor does it correspond to?
					float sensorVal = getSensorAspectValue(C.Sensor);
                    if (C.Comparator == "<")
                    {
                        allSatisfied = (sensorVal < C.Value);
                    }
                    else if (C.Comparator == ">")
						allSatisfied = (sensorVal > C.Value);
                    else if (C.Comparator == "=")
					{
						allSatisfied = (Math.Abs(sensorVal - C.Value) <= tolerance);
					}
                    else if (C.Comparator == ">=")
						allSatisfied = (sensorVal >= C.Value);
                    else if (C.Comparator == "<=")
						allSatisfied = (sensorVal <= C.Value);
                    else if (C.Comparator == "!=")
						allSatisfied = (Math.Abs(sensorVal - C.Value) > tolerance);
					else {
                        throw new Exception("Unrecognized operatorType " + C.Comparator);
					}
						
					if (!allSatisfied)
					{
						break;
					}
				}
			}
			
			//if all conditions are satisfied, trigger this reflex and send msg to the user
			if (allSatisfied)
			{
				if (GlobalVariables.sendNotificationOnReflexFirings)
					outgoingMessages.Add("reflexFired," + r.ReflexName + "\n");
				foreach (JSONAIMessage a in r.Actions)
					messageQueueJ.Add (a);
			}
		}
	}

	/// <summary>
	/// Called after first loading a file. Goes through all custom items and adds them to the list.
    /// CURRENTLY NOT IMPLEMENTED
	/// </summary>
	public void indexCustomItems()
	{
		//for every custom item on the map
		customItemController[] goArray = UnityEngine.MonoBehaviour.FindObjectsOfType(typeof(customItemController)) as customItemController[];
		Debug.Log(goArray.Length);
		foreach (customItemController c in goArray)
		{
			string wName = c.objectName;
			/*while (customItems.ContainsKey(wName))
			{
				if (customItems[wName] != null)
				{
					Destroy(customItems[wName].gameObject);
				}
				customItems.Remove(wName);
				Debug.Log("removing " + wName);
			}*/
			if (customItems.ContainsKey(wName))
			{
				//Debug.Log("seeing key " + wName + " to " + customItems[wName]);
			}
			else
			{
				//Debug.Log("adding key " + wName + " (not in dictionary)");
				customItems.Add(wName,c);
			}
		}
	}

	/// <summary>
	/// iterate through the customItems, remove any that no longer exist
	/// </summary>
	void updateCustomItems()
	{
		List<string> toRemove = new List<string> ();
		foreach (string ky in customItems.Keys)
			if (customItems [ky] == null)
				toRemove.Add (ky);
		foreach (string ky in toRemove)
			customItems.Remove (ky);
	}

	//public Camera mainCamera;
	public customItemController emptyblock; //used to instantiate custom objects
	public speechBubbleController emptyBubble; //used to instantiate speech bubbles
	public Dictionary<string,worldObject> customItems = new Dictionary<string, worldObject>();

	// Update is called once per frame. Curently Empty until confirmed that message reading is not needed in here
	void Update () {
		//update arm positions
        /// TODO: MOVE THIS TO FIXED UPDATE BECAUSE PHYSICS
		Vector2 leftRelativePoint = gameObject.GetComponent<Rigidbody2D>().GetRelativePoint(leftHand.GetComponent<DistanceJoint2D>().connectedAnchor);
		Vector3 leftAnchor = new Vector3(leftRelativePoint.x, leftRelativePoint.y);
		leftArm[0].transform.position = (leftHand.transform.position*1/3 + leftAnchor*2/3);
		leftArm[1].transform.position = (leftHand.transform.position*2/3 + leftAnchor*1/3);
		Vector2 rightRelativePoint = gameObject.GetComponent<Rigidbody2D>().GetRelativePoint(rightHand.GetComponent<DistanceJoint2D>().connectedAnchor);
		Vector3 rightAnchor = new Vector3(rightRelativePoint.x, rightRelativePoint.y);
		rightArm[0].transform.position = (rightHand.transform.position*1/3 + rightAnchor*2/3);
		rightArm[1].transform.position = (rightHand.transform.position*2/3 + rightAnchor*1/3);
		
		//updateSensors();
		
		
		//////keyboard controls///////
		
		//hand gripping
		if (Input.GetKeyDown(KeyCode.LeftShift))
			setGrip(true, true);
		if (Input.GetKeyUp(KeyCode.LeftShift))
			setGrip(true, false);
		if (Input.GetKeyDown(KeyCode.RightShift))
			setGrip(false, true);
		if (Input.GetKeyUp(KeyCode.RightShift))
			setGrip(false, false);
		
		
		if (Input.GetKeyDown(KeyCode.P))
			powerMode = !powerMode;
		float handMoveForce = 50f;
		if (powerMode)
			handMoveForce *= 10;
		//right hand
		if (Input.GetKey(KeyCode.UpArrow)) {//GetKeyDown is one-time press only
			//transform.Translate(new Vector3(1,0,0));
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(0,handMoveForce);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			rightHandRigidBody.AddForce(f);//, ForceMode2D.Impulse);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}
		if (Input.GetKey (KeyCode.DownArrow)) {
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(0,-handMoveForce);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			rightHandRigidBody.AddForce(f);//, ForceMode2D.Impulse);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}
		if (Input.GetKey(KeyCode.LeftArrow))
		{
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(-handMoveForce,0);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			rightHandRigidBody.AddForce(f);//, ForceMode2D.Impulse);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}
		if (Input.GetKey(KeyCode.RightArrow))
		{
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(handMoveForce,0);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			rightHandRigidBody.AddForce(f);//, ForceMode2D.Impulse);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}		
		//left hand
		if (Input.GetKey (KeyCode.W)) {//GetKeyDown is one-time press only
			//transform.Translate(new Vector3(1,0,0));
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(0,handMoveForce);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			leftHandRigidBody.AddForce(f);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}
		if (Input.GetKey (KeyCode.S)) {
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(0,-handMoveForce);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			leftHandRigidBody.AddForce(f);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}
		if (Input.GetKey(KeyCode.A))
		{
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(-handMoveForce,0);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			leftHandRigidBody.AddForce(f);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}
		if (Input.GetKey(KeyCode.D))
		{
			Vector2 f = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(handMoveForce,0);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
			leftHandRigidBody.AddForce(f);
			GetComponent<Rigidbody2D>().AddForce(-f);
		}
		
		
		/*if (Input.GetKeyDown(KeyCode.LeftShift))
		{
			leftHandRigidBody.AddForce(new Vector2(-1000, 0));
			rightHandRigidBody.AddForce(new Vector2(-1000, 0));
			rigidbody2D.AddForce(new Vector2(2000, 0));
		}
		if (Input.GetKeyDown(KeyCode.RightShift))
		{
			leftHandRigidBody.AddForce(new Vector2(1000, 0));
			rightHandRigidBody.AddForce(new Vector2(1000, 0));
			rigidbody2D.AddForce(new Vector2(-2000, 0));
		}*/
		
		

		if (Input.GetKeyDown(KeyCode.Space))
			jump(30000f);
		if (Input.GetKey(KeyCode.V))
			GlobalVariables.viewControlsVisible = true;
		
		//Rotate
		if (Input.GetKey(KeyCode.R))
		{
			GetComponent<Rigidbody2D>().rotation += 1.0f;
			GetComponent<Rigidbody2D>().rotation %= 360f;
			leftHandRigidBody.rotation = GetComponent<Rigidbody2D>().rotation;
			rightHandRigidBody.rotation = GetComponent<Rigidbody2D>().rotation;
			//Debug.Log (rigidbody2D.GetRelativePoint(rightAnchor));
			GetComponent<Rigidbody2D>().AddForce(new Vector2(0,0)); //this forces the screen to update his rotation
		}
		if (Input.GetKey(KeyCode.T))
		{
			GetComponent<Rigidbody2D>().rotation -= 1.0f;
			GetComponent<Rigidbody2D>().rotation %= 360f;
			leftHandRigidBody.rotation = GetComponent<Rigidbody2D>().rotation;
			rightHandRigidBody.rotation = GetComponent<Rigidbody2D>().rotation;
			//Debug.Log (rigidbody2D.GetRelativePoint(rightAnchor));
			GetComponent<Rigidbody2D>().AddForce(new Vector2(0,0)); //this forces the screen to update his rotation
		}
		
		//Move
		if (Input.GetKey(KeyCode.F))
			GetComponent<Rigidbody2D>().AddForce(GetComponent<Rigidbody2D>().transform.rotation*(new Vector2(-500f,0)));
		if (Input.GetKey(KeyCode.G))
			GetComponent<Rigidbody2D>().AddForce(transform.rotation*new Vector2(500f,0));
	}


    // Message reading and reflex checking can go in FixedUpdate since creating isntances of objects
    // should happen in there as well as moving RigidBodies
    void FixedUpdate()
    {
        checkReflexes();

        //check for messages in the message queue (which stores all messages sent by TCP clients)
        while (messageQueueJ.Count() > 0)
        {
            JSONAIMessage firstMsg;
            while (!messageQueueJ.TryGet(0, out firstMsg))
            {
                Thread.Sleep(1000);
            }

            while (!messageQueueJ.TryRemoveAt(0))
                Thread.Sleep(1000);

            try
            {
                switch (firstMsg.Type)
                {
                    case JSONAIMessage.MessageType.Error:
                        outgoingMessages.Add("UnrecognizedCommandError:" + firstMsg.Command + "\n");
                        break;
                    case JSONAIMessage.MessageType.CreateItem:
                        CreateItem msg = firstMsg as CreateItem;
                        customItemController cic = Instantiate(emptyblock, new Vector3(), new Quaternion()) as customItemController;
                        if (!cic.initialize(msg.FilePath, msg.Name, msg.Position, msg.Rotation,
                            msg.Endorphins, msg.Mass, msg.Friction, msg.Kinematic))
                        {
                            outgoingMessages.Add("createItem," + msg.Name + ",FAILED,fileNotFound\n");
                        }
                        else
                        {
                            while (customItems.Keys.Contains(msg.Name))
                            {
                                if (customItems[msg.Name] != null)
                                {
                                    Destroy(customItems[msg.Name].gameObject);
                                }

                                customItems.Remove(msg.Name);
                            }
                            customItems.Add(msg.Name, cic);

                            outgoingMessages.Add("cretateItem" + msg.Name + ",OK\n");
                        }
                        break;
                    case JSONAIMessage.MessageType.Say:
                        Say sayMsg = (Say)firstMsg;
                        updateCustomItems();
                        speechBubbleController sbc = Instantiate(emptyBubble, new Vector3(), new Quaternion()) as speechBubbleController;
                        if (sayMsg.Speaker == "P") //the speaker is PAGI guy, position vector is relative to him
                        {
                            //bubbleName = firstMsg.otherStrings[0] + "_speechBubble";
                            sbc.initialize(sayMsg.Text, sayMsg.Duration,
                                             GetComponent<Rigidbody2D>().position + sayMsg.Position);
                        }
                        else if (sayMsg.Speaker == "N") //there is no speaker; use the position given as absolute
                        {
                            sbc.initialize(sayMsg.Text, sayMsg.Duration,
                                                       sayMsg.Position);
                        }
                        else //the speaker is a custom object
                        {
                            //find the item to add speech to, add it
                            if (!customItems.ContainsKey(sayMsg.Speaker))
                            {
                                outgoingMessages.Add("say," + sayMsg.Speaker + ",ERR:Speaker_Name_Not_Found\n");
                            }
                            else
                            {
                                if (customItems[sayMsg.Speaker] == null)
                                {
                                    customItems.Remove(sayMsg.Speaker);
                                    outgoingMessages.Add("say," + sayMsg.Speaker + ",ERR:Object_Deleted\n");
                                }
                                else
                                {
                                    //create item
                                    sbc.initialize(sayMsg.Text, sayMsg.Duration,
                                                       customItems[sayMsg.Speaker].GetComponent<Rigidbody2D>().position + sayMsg.Position);
                                }
                                outgoingMessages.Add("say," + sayMsg.Speaker + ",OK\n");
                            }
                        }
                        break;

                    case JSONAIMessage.MessageType.AddForceToItem:
                        updateCustomItems();
                        AddForceToItem addforceToItem = (AddForceToItem) firstMsg;
                        //find the item to add force to, add it
                        if (!customItems.ContainsKey(addforceToItem.Name))
                        {
                            outgoingMessages.Add("addForceToItem," + addforceToItem.Name + ",ERR:Item_Name_Not_Found\n");
                        }
                        else
                        {
                            if (customItems[addforceToItem.Name] == null)
                            {
                                customItems.Remove(addforceToItem.Name);
                                outgoingMessages.Add("addForceToItem," + addforceToItem.Name + ",ERR:Object_Deleted\n");
                            }
                            else
                            {
                                customItems[addforceToItem.Name].GetComponent<Rigidbody2D>().AddForce(addforceToItem.ForceVector);
                                customItems[addforceToItem.Name].GetComponent<Rigidbody2D>().AddTorque(addforceToItem.Rotation);
                                outgoingMessages.Add("addForceToItem," + addforceToItem.Name + ",OK\n");
                            }
                        }
                        break;
                    case JSONAIMessage.MessageType.GetInfoAboutItem:
                        //find the item to add force to, add it
                        GetInfoAboutItem getInfoAboutItem = (GetInfoAboutItem)firstMsg;
                        if (!customItems.ContainsKey(getInfoAboutItem.Name))
                        {
                            outgoingMessages.Add("getInfoAboutItem," + getInfoAboutItem.Name + ",ERR:Item_Name_Not_Found\n");
                        }
                        else
                        {
                            if (customItems[getInfoAboutItem.Name] == null)
                            {
                                customItems.Remove(getInfoAboutItem.Name);
                                outgoingMessages.Add("getInfoAboutItem," + getInfoAboutItem.Name + ",ERR:Object_Deleted\n");
                            }
                            else
                            {
                                worldObject wo = customItems[getInfoAboutItem.Name];
                                string toReturn = "getInfoAboutItem," + getInfoAboutItem.Name + ",";
                                toReturn = toReturn + wo.transform.position.x.ToString() + "," + wo.transform.position.y.ToString() + ",";
                                toReturn = toReturn + wo.GetComponent<Rigidbody2D>().velocity.x.ToString() + "," + wo.GetComponent<Rigidbody2D>().velocity.y.ToString() + "\n";
                                outgoingMessages.Add(toReturn);
                            }
                        }
                        break;
                    case JSONAIMessage.MessageType.DestroyItem:
                        //find the item to add force to, add it
                        DestroyItem destroyItem = (DestroyItem)firstMsg;
                        if (!customItems.ContainsKey(destroyItem.Name))
                        {
                            outgoingMessages.Add("destroyItem," + destroyItem.Name + ",ERR:Item_Name_Not_Found\n");
                        }
                        else
                        {
                            if (customItems[destroyItem.Name] == null)
                            {
                                customItems.Remove(destroyItem.Name);
                                outgoingMessages.Add("destroyItem," + destroyItem.Name + ",WARNING:Object_Already_Deleted\n");
                            }
                            else
                            {
                                Destroy(customItems[destroyItem.Name].gameObject);
                                customItems.Remove(destroyItem.Name);
                                outgoingMessages.Add("destroyItem," + destroyItem.Name + ",OK\n");
                            }
                        }
                        break;
                    case JSONAIMessage.MessageType.Print:
                        Print print = (Print)firstMsg;
                        outgoingMessages.Add("print,OK\n");
                        break;
                    case JSONAIMessage.MessageType.FindObj:
                        FindObj findObj = (FindObj)firstMsg;
                        string findObjToReturn = "findObj," + findObj.Name;
                        string searchType = (findObj.Model).Trim();
                        if (searchType == "D" || searchType == "PD")
                        {
                            foreach (visualSensor v in visualSensors)
                            {
                                v.updateSensor();
                                if (v.name.Trim() == findObj.Name.Trim())
                                    findObjToReturn += ",V" + v.indexX.ToString() + "." + v.indexY.ToString();
                            }
                        }
                        if (searchType == "P" || searchType == "PD")
                        {
                            foreach (visualSensor p in peripheralSensors)
                            {
                                p.updateSensor();
                                if (p.name.Trim() == findObj.Name.Trim())
                                    findObjToReturn += ",P" + p.indexX.ToString() + "." + p.indexY.ToString();
                            }
                        }
                        outgoingMessages.Add(findObjToReturn + "\n");
                        break;

                    case JSONAIMessage.MessageType.LoadTask:
                        LoadTask loadTask = (LoadTask)firstMsg;
                        findObjToReturn = "loadTask," + loadTask.File.Trim();
                        //remove all world objects currently in scene (except for body/hands)
                        worldObject[] goArray = UnityEngine.MonoBehaviour.FindObjectsOfType(typeof(worldObject)) as worldObject[];
                        List<string> doNotRemove = new List<string>() { "leftHand", "rightHand", "mainBody" };
                        foreach (worldObject obj in goArray)
                        {
                            if (!doNotRemove.Contains(obj.objectName))
                            {
                                Destroy(obj.gameObject);
                            }
                        }
                        bool loadedOk = true;
                        try
                        {
                            FileSaving fs = new FileSaving(loadTask.File);
                        }
                        catch (Exception e)
                        {
                            string errDesc = e.ToString().Replace('\n', ';');
                            errDesc = errDesc.Replace('\r', ' ');
                            outgoingMessages.Add(findObjToReturn + ",ERR," + errDesc + "\n");
                            loadedOk = false;
                        }
                        if (loadedOk)
                            outgoingMessages.Add(findObjToReturn + ",OK\n");
                        break;

                    case JSONAIMessage.MessageType.SetState:
                        //is the state already active? If so, replace the time
                        SetState setState = (SetState)firstMsg;
                        State foundState = null;
                        foreach (State st in GlobalVariables.activeStates.getCopy())
                        {
                            if (st.stateName == setState.Name)
                            {
                                foundState = st;
                                break;
                            }
                        }
                        if (foundState != null)
                        {
                            //replace state
                            GlobalVariables.activeStates.TryRemove(foundState);
                        }
                        if (setState.State.lifeTime != TimeSpan.Zero)
                        {
                            Debug.Log("Adding State");
                            GlobalVariables.activeStates.Add(setState.State);
                        }
                        outgoingMessages.Add("stateUpdated," + setState.Name.Trim() + "\n");
                        break;

                    case JSONAIMessage.MessageType.GetActiveReflexes:
                        string toR = "activeReflexes";
                        foreach (ReflexJ r in GlobalVariables.activeReflexes.getCopy())
                        {
                            toR += "," + r.ReflexName;
                        }
                        outgoingMessages.Add(toR + "\n");
                        break;

                    case JSONAIMessage.MessageType.GetActiveStates:
                        toR = "activeStates:";
                        List<State> allStates = GlobalVariables.activeStates.getCopy();
                        foreach (State sta in allStates)
                        {
                            toR += sta.stateName + ",";
                        }
                        if (allStates.Count == 0)
                            toR += "(none)";
                        else
                            toR = toR.Substring(0, toR.Length - 1);
                        outgoingMessages.Add(toR + "\n");
                        break;
                    case JSONAIMessage.MessageType.RemoveReflex:
                        ReflexJ re = null;
                        RemoveReflex rmReflex = (RemoveReflex)firstMsg;
                        foreach (ReflexJ R in GlobalVariables.activeReflexes.getCopy())
                        {
                            if (R.ReflexName.Trim() == rmReflex.Name.Trim())
                            {
                                re = R;
                                break;
                            }
                        }
                        if (re != null)
                        {
                            GlobalVariables.activeReflexes.TryRemove(re);
                            outgoingMessages.Add("removedReflex," + rmReflex.Name.Trim() + ",OK\n");
                        }
                        else
                            outgoingMessages.Add("removedReflexFAILED" + rmReflex.Name.Trim() + ",FAILED\n");

                        break;
                    case JSONAIMessage.MessageType.SetReflex:
                        //does a reflex with this name already exist? If so, replace it
                        SetReflex setReflex = (SetReflex)firstMsg;
                        re = null;
                        foreach (ReflexJ R in GlobalVariables.activeReflexes.getCopy())
                        {
                            if (R.ReflexName.Trim() == setReflex.Name.Trim())
                            {
                                re = R;
                                break;
                            }
                        }
                        if (re != null)
                            GlobalVariables.activeReflexes.TryRemove(re);

                        GlobalVariables.activeReflexes.Add(setReflex.Reflex);
                        outgoingMessages.Add("reflexUpdated," + setReflex.Name.Trim() + "\n");
                        break;

                    case JSONAIMessage.MessageType.DropItem:
                        //if required, there is additional content at firstMsg.detail
                        //find the asset that matches the name
                        bool loaded = false;
                        DropItem dropItem = (DropItem)firstMsg;
                        foreach (worldObject s in Resources.LoadAll<worldObject>("Prefabs"))
                        {
                            if (s.objectName == dropItem.Name.Trim())
                            {
                                worldObject newObj = MonoBehaviour.Instantiate(s,//Resources.Load<GameObject>(wo.assetPath) 
                                                                                new Vector3(dropItem.Position.x, dropItem.Position.y),
                                                                                new Quaternion()) as worldObject;
                                loaded = true;
                                break;
                            }
                        }
                        if (!loaded)
                            outgoingMessages.Add("dropItem," + dropItem.Name + ",FAILED:obj_not_found\n");
                        else
                            outgoingMessages.Add("dropItem," + dropItem.Name + ",OK\n");
                        break;
                    case JSONAIMessage.MessageType.AddForce:
                        //do we need to evaluate the force value, e.g. if there is a function?
                        AddForce addForce = (AddForce)firstMsg;
                        switch (addForce.Effector)
                        {
                            case "TEST":
                                /*string s = "";
                                int numToSend = int.Parse(firstMsg.floatContent.ToString());
                                Debug.Log("creating string ("+numToSend.ToString()+")");
                                for (int i=0; i<numToSend; i++)
                                    s += "X";*/
                                //Debug.Log("got TEST msg: " + firstMsg.function1.evaluate(getSensorAspectValue));
                                //outgoingMessages.Add(s+'\n');
                                break;
                            /*LHV,LHH - Left hand vertical and horizontal. v is the amount of force (positive or negative) to add in each dimension.
                            RHV,RHH - Right hand vertical and horizontal. v is the amount of force (positive or negative) to add in each dimension.
                            BMV,BMH - Body vertical and horizontal. v is the amount of force (positive or negative) to add in each dimension.
                            BR - Body rotation right or left. v is the amount of torque to use to rotate the body (can be positive or negative).
                            RHG,RHR - Right hand grip and release. v is required, but ignored here. A hand is either in a state of gripping or it isn't.
                            LHG,LHR - Left hand grip and release. v is required, but ignored here. A hand is either in a state of gripping or it isn't.*/
                            case "LHH":
                                Vector2 f = GetComponent<Rigidbody2D>().transform.rotation * new Vector2(addForce.Force.evaluate(getSensorAspectValue), 0);//new Vector2(Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
                                leftHandRigidBody.AddForce(f);
                                GetComponent<Rigidbody2D>().AddForce(-f);
                                //rigidbody2D.AddForce(new Vector2(0, 10000));
                                outgoingMessages.Add("LHH,1\n");
                                break;
                            case "LHV":
                                f = GetComponent<Rigidbody2D>().transform.rotation * new Vector2(0, addForce.Force.evaluate(getSensorAspectValue));//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
                                leftHandRigidBody.AddForce(f);//, ForceMode2D.Impulse);
                                GetComponent<Rigidbody2D>().AddForce(-f);
                                outgoingMessages.Add("LHV,1\n");
                                break;
                            case "LHvec":
                                f = GetComponent<Rigidbody2D>().transform.rotation *
                                    (new Vector2(addForce.HorizontalForce.evaluate(getSensorAspectValue), addForce.VerticalForce.evaluate(getSensorAspectValue)));
                                leftHandRigidBody.AddForce(f);//, ForceMode2D.Impulse);
                                GetComponent<Rigidbody2D>().AddForce(-f);
                                outgoingMessages.Add("LHvec,1\n");
                                break;
                            case "RHH":
                                f = GetComponent<Rigidbody2D>().transform.rotation * new Vector2(addForce.Force.evaluate(getSensorAspectValue), 0);//new Vector2(Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
                                rightHandRigidBody.AddForce(f);
                                GetComponent<Rigidbody2D>().AddForce(-f);
                                outgoingMessages.Add("RHH,1\n");
                                break;
                            case "RHV":
                                f = GetComponent<Rigidbody2D>().transform.rotation * new Vector2(0, addForce.Force.evaluate(getSensorAspectValue));//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
                                rightHandRigidBody.AddForce(f);//, ForceMode2D.Impulse);
                                GetComponent<Rigidbody2D>().AddForce(-f);
                                outgoingMessages.Add("RHV,1\n");
                                break;
                            case "RHvec":
                                f = GetComponent<Rigidbody2D>().transform.rotation *
                                    (new Vector2(addForce.HorizontalForce.evaluate(getSensorAspectValue), addForce.VerticalForce.evaluate(getSensorAspectValue))); ;
                                GetComponent<Rigidbody2D>().AddForce(-f);
                                rightHandRigidBody.AddForce(f);
                                outgoingMessages.Add("RHvec,1\n");
                                break;
                            case "BMH":
                                f = GetComponent<Rigidbody2D>().transform.rotation * new Vector2(addForce.Force.evaluate(getSensorAspectValue), 0);//new Vector2(Mathf.Cos(Mathf.Deg2Rad*rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Sin(Mathf.Deg2Rad*rigidbody2D.rotation)*firstMsg.floatContent);
                                GetComponent<Rigidbody2D>().AddForce(f);
                                outgoingMessages.Add("BMH,1\n");
                                break;
                            case "BMV":
                                f = GetComponent<Rigidbody2D>().transform.rotation * new Vector2(0, addForce.Force.evaluate(getSensorAspectValue));//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*firstMsg.floatContent);
                                GetComponent<Rigidbody2D>().AddForce(f);
                                outgoingMessages.Add("BMV,1\n");
                                break;
                            case "BMvec":
                                f = GetComponent<Rigidbody2D>().transform.rotation *
                                    (new Vector2(addForce.HorizontalForce.evaluate(getSensorAspectValue), addForce.VerticalForce.evaluate(getSensorAspectValue)));
                                GetComponent<Rigidbody2D>().AddForce(f);
                                outgoingMessages.Add("BMvec,1\n");
                                break;

                            case "J": //jump
                                bool foundGround = jump(30000f);
                                if (foundGround)
                                    outgoingMessages.Add("J,1\n");
                                else
                                    outgoingMessages.Add("J,0\n");
                                break;
                            case "BR":
                                GetComponent<Rigidbody2D>().rotation += addForce.Force.evaluate(getSensorAspectValue);
                                leftHand.GetComponent<Rigidbody2D>().rotation = GetComponent<Rigidbody2D>().rotation;
                                rightHand.GetComponent<Rigidbody2D>().rotation = GetComponent<Rigidbody2D>().rotation;
                                GetComponent<Rigidbody2D>().AddForce(Vector2.zero); //forces update of rotation
                                outgoingMessages.Add("BR,1\n");
                                break;
                            case "RHG":
                                setGrip(false, true);
                                outgoingMessages.Add("RHG,1\n");
                                break;
                            case "RHR":
                                setGrip(false, false);
                                outgoingMessages.Add("RHR,1\n");
                                break;
                            case "LHG":
                                setGrip(true, true);
                                outgoingMessages.Add("LHG,1\n");
                                break;
                            case "LHR":
                                setGrip(true, false);
                                outgoingMessages.Add("LHR,1\n");
                                break;
                        }
                        break;
                    case JSONAIMessage.MessageType.SensorRequest:
                        SensorRequest sensorRequest = (SensorRequest)firstMsg;
                        Debug.Log("checking sensor value " + sensorRequest.Sensor);
                        switch (sensorRequest.Sensor[0])
                        {
                            case 'M': //a full map of the visual field
                                if (sensorRequest.Sensor.Trim() == "MDN") //detailed visual field (names only)
                                {
                                    StringBuilder sb = new StringBuilder("MDN,");
                                    //string toReturn = "MDN,";
                                    for (int y = 0; y < numVisualSensorsY; y++)
                                    {
                                        for (int x = 0; x < numVisualSensorsX; x++)
                                        {
                                            visualSensor s = visualSensors[x, y];
                                            s.updateSensor();
                                            string sName = s.name;
                                            if (sName == "Background")
                                                sName = "";
                                            sb.Append(sName + ",");
                                        }
                                    }
                                    sb[sb.Length - 1] = '\n';
                                    outgoingMessages.Add(sb.ToString());
                                }
                                else if (sensorRequest.Sensor.Trim() == "MPN") //peripheral visual field (names only)
                                {
                                    StringBuilder sb = new StringBuilder("MPN,");
                                    int count = 0;
                                    for (int y = 0; y < numPeripheralSensorsY; y++)
                                    {
                                        for (int x = 0; x < numPeripheralSensorsX; x++)
                                        {
                                            visualSensor s = peripheralSensors[x, y];
                                            s.updateSensor();
                                            string sName = s.name;
                                            if (sName == "Background")
                                                sName = "";
                                            sb.Append(sName + ",");
                                            count++;
                                        }
                                    }
                                    sb[sb.Length - 1] = '\n';
                                    outgoingMessages.Add(sb.ToString());
                                }
                                else
                                    outgoingMessages.Add("sensorRequest,UNRECOGNIZED_SENSOR_ERROR:" + sensorRequest.Sensor.Trim() + "\n");
                                break;
                            case 'B': //body touch sensor B0-B7
                                if (sensorRequest.Sensor[1] == 'P') //body position
                                {
                                    Vector2 v = GetComponent<Rigidbody2D>().position;
                                    outgoingMessages.Add("BP," + v.x.ToString() + "," + v.y.ToString() + "\n");
                                }
                                else
                                {
                                    int sensorNum = int.Parse(sensorRequest.Sensor[1].ToString());
                                    touchSensor sensor = bodySensor[sensorNum];
                                    sensor.updateSensor();
                                    outgoingMessages.Add("B" + sensorNum.ToString() + "," + sensor.getReport() + "\n");
                                }
                                break;
                            case 'S': //speed sensor
                                Vector2 sV = GetComponent<Rigidbody2D>().GetRelativePointVelocity(Vector2.zero);
                                outgoingMessages.Add("S," + sV.x.ToString() + "," + sV.y.ToString() + "\n");
                                break;
                            case 'L': //L0-L4, or LP
                                if (sensorRequest.Sensor[1] == 'P')
                                {//proprioception; get sensor position relative to body
                                    leftHandSensor[4].updateSensor(); //recall sensor 4 is right in the middle of the hand
                                    Vector2 relativePoint = GetComponent<Rigidbody2D>().GetPoint(leftHandSensor[4].getPosition());
                                    outgoingMessages.Add("LP," + relativePoint.x.ToString() + "," + relativePoint.y.ToString() + "\n");

                                }
                                else
                                {
                                    int sensorNum = int.Parse(sensorRequest.Sensor[1].ToString());
                                    touchSensor sensor = leftHandSensor[sensorNum];
                                    sensor.updateSensor();
                                    outgoingMessages.Add("L" + sensorNum.ToString() + "," + sensor.getReport() + "\n");
                                }//test
                                break;
                            case 'R': //R0-R4, or RP
                                if (sensorRequest.Sensor[1] == 'P')
                                {//proprioception; get sensor position relative to body
                                    rightHandSensor[4].updateSensor();
                                    Vector2 relativePoint = GetComponent<Rigidbody2D>().GetPoint(rightHandSensor[4].getPosition());
                                    outgoingMessages.Add("RP," + relativePoint.x.ToString() + "," + relativePoint.y.ToString() + "\n");
                                }
                                else
                                {
                                    int sensorNum = int.Parse(sensorRequest.Sensor[1].ToString());
                                    touchSensor sensor = rightHandSensor[sensorNum];
                                    sensor.updateSensor();
                                    outgoingMessages.Add("R" + sensorNum.ToString() + "," + sensor.getReport() + "\n");
                                }
                                break;
                            case 'V': //visual sensor V0.0 - V30.20
                                string[] tmp = sensorRequest.Sensor.Substring(1).Split('.');
                                int vX = int.Parse(tmp[0]);
                                int vY = int.Parse(tmp[1]);
                                if (vX >= numVisualSensorsX || vY >= numVisualSensorsY)
                                {
                                    outgoingMessages.Add("sensorRequest," + sensorRequest.Sensor + ",ERR:IndexOutOfRange\n");
                                }
                                visualSensor vVS = visualSensors[vX, vY];
                                vVS.updateSensor();
                                string response = sensorRequest.Sensor.Trim();
                                for (int i = 0; i < vVS.vq.Length; i++)
                                    response += "," + vVS.vq[i].ToString();
                                response += "," + vVS.type + "," + vVS.name + "\n";
                                outgoingMessages.Add(response);
                                break;
                            case 'P': //peripheral sensor V0.0 - V15.10
                                tmp = sensorRequest.Sensor.Substring(1).Split('.');
                                vX = int.Parse(tmp[0]);
                                vY = int.Parse(tmp[1]);
                                if (vX >= numPeripheralSensorsX || vY >= numPeripheralSensorsY)
                                {
                                    outgoingMessages.Add("sensorRequest," + sensorRequest.Sensor + ",ERR:IndexOutOfRange\n");
                                }
                                vVS = peripheralSensors[vX, vY];
                                vVS.updateSensor();
                                response = sensorRequest.Sensor.Trim();
                                for (int i = 0; i < vVS.vq.Length; i++)
                                    response += "," + vVS.vq[i].ToString();
                                response += "," + vVS.type + "," + vVS.name + "\n";
                                outgoingMessages.Add(response);
                                break;
                            case 'A': //rotation sensor
                                outgoingMessages.Add("A," + (Mathf.Deg2Rad * GetComponent<Rigidbody2D>().rotation).ToString() + "\n");
                                break;
                            default:
                                outgoingMessages.Add("sensorRequest,UNRECOGNIZED_SENSOR_ERROR:" + sensorRequest.Sensor.Trim() + "\n");
                                break;
                        }
                        break;
                    //case AIMessage.AIMessageType.establishConnection:
                    //    break;
                    //case AIMessage.AIMessageType.removeConnection:
                    //    break;
                    default:
                        break;

                }
            }
            catch (Exception e)
            {
                outgoingMessages.Add("ERR: While processing message of type " + firstMsg.Type + " (see log)");
            }
        }
    }

    // Late Update is for repositioning cameras
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            Camera.main.transform.Translate((transform.position - Camera.main.transform.position) - new Vector3(0, 0, 10));
        }
        else
        {
            Debug.Log("no camera");
        }
    }
	
	bool jump(float amt)
	{
		//is he touching anything on the ground? Calculate five contact points between bodySensors 3 and 5.
		Vector2[] bottom = new Vector2[]{GetComponent<Rigidbody2D>().GetRelativePoint(new Vector2(0.9f, -0.9f)),
			GetComponent<Rigidbody2D>().GetRelativePoint(new Vector2(0.5f, -1.2f)),
			GetComponent<Rigidbody2D>().GetRelativePoint(new Vector2(0, -1.2f)),
			GetComponent<Rigidbody2D>().GetRelativePoint(new Vector2(-0.5f, -1.2f)),
			GetComponent<Rigidbody2D>().GetRelativePoint(new Vector2(-0.9f, -0.9f))};
		//int layerNum = 8; //Normal objects layer
        Rigidbody2D[] goArray = UnityEngine.MonoBehaviour.FindObjectsOfType(typeof(Rigidbody2D)) as Rigidbody2D[];
		bool foundGround = false;
		int triggerBoxLayer = LayerMask.NameToLayer("Trigger Boxes");
		for (int n=0; n<bottom.Length; n++)
		{
			for (int i = 0; i < goArray.Length; i++) {
				if (goArray[i] == leftHandRigidBody || goArray[i] == rightHandRigidBody)
					continue;
				if (goArray[i].gameObject.layer == triggerBoxLayer) 
					continue;
				//goList.Add(goArray[i]);
				if (goArray[i].GetComponent<Collider2D>().OverlapPoint(bottom[n]))
				{//connect it at that point
					Rigidbody2D obj = goArray[i];
					Vector2 jumpForce = GetComponent<Rigidbody2D>().transform.rotation*new Vector2(0f,amt);
					GetComponent<Rigidbody2D>().AddForce(jumpForce);//new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*30000f,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*30000f));
					obj.AddForce(-jumpForce);//-new Vector2(Mathf.Sin(Mathf.Deg2Rad*-rigidbody2D.rotation)*30000f,Mathf.Cos(Mathf.Deg2Rad*-rigidbody2D.rotation)*30000f));
					foundGround = true;
					break;
				}
				//}
			}
			if (foundGround)
				break;
		}
		return foundGround;
		
	}
	
	
	/// <summary>
	/// Sets the grip.
	/// </summary>
	/// <param name="forLeftHand">If this is for left hand, otherwise it's for the right hand</param>
	/// <param name="isGrasp">If we want this hand to grasp, otherwise it's to release</param>
	void setGrip(bool forLeftHand, bool isGrasp)
	{
		int handIndex;
		Animator a;
		Rigidbody2D handRigidBody;
		if (forLeftHand)
		{
			handIndex = 0;
			a = leftHand.GetComponent<Animator>();
			handRigidBody = leftHandRigidBody;
		}
		else
		{
			handIndex = 1;
			a = rightHand.GetComponent<Animator>();
			handRigidBody = rightHandRigidBody;
		}
		
		//is it currently touching any objects that are trigger boxes? If so, notify them
		triggerBoxController[] w = FindObjectsOfType<triggerBoxController>();
		foreach (triggerBoxController o in w)
		{
			if (o.GetComponent<Collider2D>().OverlapPoint(handRigidBody.position))
				o.GripOrReleaseHandler(isGrasp, forLeftHand);
		}
		
		if (isGrasp)
		{
			if (!handIsClosed[handIndex])
			{
				//close hand, grip whatever
				handIsClosed[handIndex] = true;
				a.SetBool("handClosed", true);
				//find all objects it might possibly grip
				
				int layerNum = 8; //Normal objects layer
				worldObject[] goArray = FindObjectsOfType(typeof(worldObject)) as worldObject[];
				List<System.Object> goList = new List<System.Object>();
				for (int i = 0; i < goArray.Length; i++) {
					if (goArray[i].gameObject.layer == layerNum) {
						//goList.Add(goArray[i]);
						if (goArray[i].GetComponent<Collider2D>().OverlapPoint(handRigidBody.position))
							
						{
							//connect it at that point
							worldObject obj = goArray[i];
							handJoint[handIndex] = obj.gameObject.AddComponent<DistanceJoint2D>();
							handJoint[handIndex].anchor = obj.GetComponent<Rigidbody2D>().GetPoint(handRigidBody.position);
							handJoint[handIndex].connectedBody = handRigidBody;
							handJoint[handIndex].connectedAnchor = new Vector2(0,0);//Vector2.zero; //the position on the hand that grabs it
							handJoint[handIndex].distance = 0;
						}
					}
				}
			}
		}
		else
		{
			if (handIsClosed[handIndex])
			{//open hand
				handIsClosed[handIndex] = false;
				a.SetBool("handClosed", false);
				//drop object
				if (handJoint[handIndex]!=null)
				{
					Destroy(handJoint[handIndex]);
					handJoint[handIndex] = null;
				}
			}
		}
	}
	
	void OnTouchedRewardOrPunishment(float msg)
	{
        //Debug.Log("endorphins " + msg);
	}
}
