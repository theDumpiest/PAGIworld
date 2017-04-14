using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MessageUtility {

    private static MessageUtility _instance = null;
    public ThreadSafeList<string> outgoingMessages = GlobalVariables.outgoingMessages;


    public customItemController emptyblock; //used to instantiate custom objects
    public Dictionary<string, worldObject> customItems = new Dictionary<string, worldObject>();


    public MessageUtility() { }

    /// <summary>
    /// iterate through the customItems, remove any that no longer exist
    /// </summary>
    public void updateCustomItems()
    {
        List<string> toRemove = new List<string>();
        foreach (string ky in customItems.Keys)
            if (customItems[ky] == null)
                toRemove.Add(ky);
        foreach (string ky in toRemove)
            customItems.Remove(ky);
    }

    

    public static MessageUtility Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new MessageUtility();
            }

            return _instance;
        }
    }

    public void error(JSONAIMessage msg)
    {
        outgoingMessages.Add("UnrecognizedCommandError:" + msg.Command + "\n");
    }

    public void createItem(JSONAIMessage firstMsg)
    {
        CreateItem msg = firstMsg as CreateItem;
        customItemController cic = UnityEngine.Object.Instantiate(emptyblock, new Vector3(), new Quaternion()) as customItemController;
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
                    UnityEngine.Object.Destroy(customItems[msg.Name].gameObject);
                }

                customItems.Remove(msg.Name);
            }
            customItems.Add(msg.Name, cic);

            outgoingMessages.Add("cretateItem" + msg.Name + ",OK\n");
        }
    }

    public void addForceToItem(JSONAIMessage firstMsg)
    {
        updateCustomItems();
        AddForceToItem addforceToItem = (AddForceToItem)firstMsg;
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
    }

    public void getInfoAboutItem(JSONAIMessage firstMsg)
    {
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
    }

    public void destroyItem(JSONAIMessage firstMsg)
    {
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
                UnityEngine.Object.Destroy(customItems[destroyItem.Name].gameObject);
                customItems.Remove(destroyItem.Name);
                outgoingMessages.Add("destroyItem," + destroyItem.Name + ",OK\n");
            }
        }
    }

    public void print(JSONAIMessage firstMsg)
    {
        Print print = (Print)firstMsg;
        outgoingMessages.Add("print,OK\n");
    }

    public void loadTask(JSONAIMessage firstMsg)
    {
        LoadTask loadTask = (LoadTask)firstMsg;
        string findObjToReturn = "loadTask," + loadTask.File.Trim();
        //remove all world objects currently in scene (except for body/hands)
        worldObject[] goArray = UnityEngine.MonoBehaviour.FindObjectsOfType(typeof(worldObject)) as worldObject[];
        List<string> doNotRemove = new List<string>() { "leftHand", "rightHand", "mainBody" };
        foreach (worldObject obj in goArray)
        {
            if (!doNotRemove.Contains(obj.objectName))
            {
                UnityEngine.Object.Destroy(obj.gameObject);
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
    }

    public void dropItem(JSONAIMessage firstMsg)
    {
        //if required, there is additional content at firstMsg.detail
        //find the asset that matches the name
        Debug.Log("WOWZA'S WE DUN DECOUPLED IT");
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
    }
}
