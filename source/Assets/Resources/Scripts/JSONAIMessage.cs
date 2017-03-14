using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

[Serializable]
/// <summary>
/// Describes the nature of messages sent from the AI controller to the world and agent.
/// All Commands built from messages will inherit from this. Main functionality is to convert
/// JSON messages into objects able to be interpretted by the world.
/// </summary>
public class JSONAIMessage
{
    public enum MessageType
    {
        Error, DropItem, SensorRequest, Say,
        Print, LoadTask, FindObj, AddForce, SetState, SetReflex, RemoveReflex,
        GetActiveStates, GetActiveReflexes, CreateItem, AddForceToItem,
        GetInfoAboutItem, DestroyItem
    }

    // Command associated with a message type
    public string Command;
    // Type of command. Used for easy switch statements
    public JSONAIMessage.MessageType Type;



    /// <summary>
    /// Converts a given string from the AI controller into a Command by creating a potential message (DumpMessage)
    /// and passing that to CreateMessage, which does the bulk of the interpretting
    /// </summary>
    /// <param name="s">JSON string taken from the socket</param>
    /// <returns></returns>
    public static JSONAIMessage fromString(string s)
    {
        if (s.Trim() == "")
        {
            throw new Exception("ERR: Received string that was nothing but whitespace!");
        }

        try
        {
            DumpMessage dump = JsonUtility.FromJson<DumpMessage>(s);
            Debug.Log(dump.Command);

            return JSONAIMessage.CreateMessage(dump);
        }
        // Typically will be caught if there was a syntax error in the JSON string, e.g. missing a bracket somewhere
        catch (ArgumentException e)
        {
            Debug.Log(e.Message);
            Debug.Log("ARGUMENT EXCEPTION");
            return new ErrorMessage()
            {
                Message = "Unable to parse command"
            };
        }
    }

    /// <summary>
    /// translates a potential message into a Command by deducing the type and building a Command
    /// from the expected information for that type.
    /// </summary>
    /// <param name="m">a DumpMessage, contains all relavent information for Command building</param>
    /// <returns></returns>
    private static JSONAIMessage CreateMessage(DumpMessage m)
    {
        // Since all Commands share this, we can use this to identify the type
        string command = m.Command;

        if (command == null || command == "")
        {
            throw new Exception("All Commands must have Command Property");
        }

        switch (command)
        {
            case "dropItem":
                DropItem dI = new DropItem()
                {
                    Command = command,
                    Type = MessageType.DropItem,
                    Name = m.Name,
                    Position = m.Position,
                    Detail = m.Detail
                };

                if (dI.Name == null || dI.Name == "" || dI.Position == null)
                {
                    throw new Exception("Incorrect # of arguments in command");
                }
                return dI;

            case "sensorRequest":
                SensorRequest sR = new SensorRequest()
                {
                    Command = command,
                    Type = MessageType.SensorRequest,
                    Sensor = m.Sensor
                };

                if (sR.Sensor == null || sR.Sensor == "")
                {
                    throw new Exception("Sensor name cannot be empty.");
                }
                return sR;

            case "say":
                Say s = new Say()
                {
                    Command = command,
                    Type = MessageType.Say,
                    Speaker = m.Speaker,
                    Duration = m.Duration,
                    Text = m.Text,
                    Position = m.Position
                };
                if (s.Speaker == null || s.Duration == null || s.Text == null || s.Position == null)
                {
                    throw new Exception("Incorrect number of arguments.");
                }
                return s;

            case "print":
                Print p = new Print()
                {
                    Command = command,
                    Type = MessageType.Print,
                    Text = m.Text
                };
                return p;

            case "loadTask":
                LoadTask lT = new LoadTask()
                {
                    Command = command,
                    Type = MessageType.LoadTask,
                    File = m.File
                };

                if (lT.File == null || lT.File == "")
                {
                    throw new Exception("Incorrect Number of arguments");
                }
                return lT;

            case "findObj":
                FindObj fO = new FindObj()
                {
                    Command = command,
                    Type = MessageType.FindObj,
                    Name = m.Name,
                    Model = m.Model
                };

                if (fO.Name == "" || fO.Name == null || fO.Model == "" || fO.Model == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }
                return fO;

            case "addForce":
                AddForce aF = null;

                // TODO: CONVERT FORCE TO VECTOR AND REMOVE HOR/VERT FORCES 
                if (m.Force != null)
                {
                    aF = new AddForce()
                    {
                        Command = command,
                        Type = MessageType.AddForce,
                        Effector = m.Effector,
                        Force = fnNode.parseFloat(m.Force)

                    };
                }

                // If Force is null, then it has to be using a HorizontalForce and VerticalForce
                else
                {
                    aF = new AddForce()
                    {
                        Command = command,
                        Type = MessageType.AddForce,
                        Effector = m.Effector,
                        HorizontalForce = fnNode.parseFloat(m.HorizontalForce),
                        VerticalForce = fnNode.parseFloat(m.VerticalForce)

                    };
                }


                if (aF.Effector == null || aF.Force == null ||
                    (aF.HorizontalForce == null ^ aF.VerticalForce == null)) // Apparently ^ is xor in C#
                {
                    throw new Exception("Incorrect number of arguments");
                }
                return aF;

            case "setState":
                SetState sS = new SetState()
                {
                    Command = command,
                    Type = MessageType.SetState,
                    Name = m.Name,
                    Duration = m.Duration
                };

                if (sS.Name == null || sS.Duration == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }

                if (sS.Duration == 0)
                {
                    sS.State = new State(sS.Name, TimeSpan.Zero);
                }
                else if (sS.Duration < 0)
                {
                    sS.State = new State(sS.Name, TimeSpan.MaxValue);
                }

                else
                {
                    sS.State = new State(sS.Name, new TimeSpan(0, 0, 0, 0, sS.Duration));
                }
                return sS;

            case "setReflex":
                SetReflex sRe = new SetReflex();
                sRe.Command = command;
                sRe.Type = MessageType.SetReflex;
                sRe.Name = m.Name;
                sRe.Reflex = new ReflexJ(sRe.Name);
                if (m.Conditions != null)
                {
                    string regexMatch = @"[<>=!]"; // Regex for comparison operators

                    // Sensory conditions should have some kind of comparison operator in them,
                    // So this regex looks for all possible comparators. Those that match must be
                    // sensory conditions, and those that don't must be state conditions
                    Regex senseMatch = new Regex(regexMatch);
                    List<string> sensoryConditions = m.Conditions.Where(f => senseMatch.IsMatch(f)).ToList();
                    List<string> stateConditions = m.Conditions.Where(f => !senseMatch.IsMatch(f)).ToList();

                    // Handle Sensory Condition collections
                    foreach (string sense in sensoryConditions)
                    {
                        // Split the sense into its parts based on the comparator.
                        // The first result will be our sensor, and the second result will
                        // be the value
                        string[] splitResults = Regex.Split(sense, regexMatch);
                        // Since Split adds in empty strings into the results, we need to remove those
                        splitResults = splitResults.Where(f => f != "").ToArray();
                        // Since comparators have potentially many components (e.g. '<' vs "<="),
                        // we need to comb through and collect all of the regex matches into a string the
                        // represents the full comparator
                        StringBuilder sb = new StringBuilder();
                        foreach (Match mat in Regex.Matches(sense, regexMatch))
                        {
                            sb.Append(mat.Value);
                        }
                        // Collect our information
                        string comparator = sb.ToString();
                        string sensor = splitResults[0];
                        float value = float.Parse(splitResults[1]);
                        Debug.Log(string.Format("Sensory Condition: {0}, {1}, {2}", sensor, comparator, value));
                        sRe.Reflex.addCondition(sensor, comparator, value);
                    }

                    // Handle state condition information
                    foreach (string state in stateConditions)
                    {
                        string stateName = "";
                        bool negated = false;

                        // If state condition is negated, set flag appropriately.
                        // This also means that the actual state name starts at
                        // the 1st index of the string, not the 0th.
                        if (state[0] == '-')
                        {
                            negated = true;
                            stateName = state.Substring(1);
                        }

                        else
                        {
                            stateName = state;
                        }

                        sRe.Reflex.addCondition(stateName, negated);

                    }
                }

                if (m.Actions != null)
                {
                    sRe.Reflex.Actions = m.Actions.Select(x => JSONAIMessage.CreateMessage(x)).ToList();
                }

                if (sRe.Reflex.ReflexName == null || sRe.Reflex.Conditions == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }

                return sRe;

            case "removeReflex":
                RemoveReflex rR = new RemoveReflex()
                {
                    Command = m.Command,
                    Type = MessageType.RemoveReflex,
                    Name = m.Reflex
                };
                if (rR.Name == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }
                return rR;

            case "getActiveStates":
                GetActiveStates gAS = new GetActiveStates()
                {
                    Command = command,
                    Type = MessageType.GetActiveStates
                };
                return gAS;

            case "getActiveReflexes":
                GetActiveReflexes gAR = new GetActiveReflexes()
                {
                    Command = command,
                    Type = MessageType.GetActiveReflexes
                };
                return gAR;

            case "createItem":
                CreateItem cI = new CreateItem()
                {
                    Command = command,
                    Type = MessageType.CreateItem,
                    Name = m.Name,
                    FilePath = m.FilePath,
                    Position = m.Position,
                    Mass = m.Mass,
                    Friction = m.Friction,
                    Rotation = m.Rotation,
                    Endorphins = m.Endorphins,
                    Kinematic = m.Kinematic
                };

                if (cI.Name == null || cI.FilePath == null || cI.Position == null
                    || cI.Mass == null || cI.Friction == null || cI.Rotation == null
                    || cI.Endorphins == null || cI.Kinematic == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }
                return cI;

            case "addForceToItem":
                AddForceToItem aFTI = new AddForceToItem()
                {
                    Command = command,
                    Type = MessageType.AddForceToItem,
                    Name = m.Name,
                    ForceVector = m.ForceVector,
                    Rotation = m.Rotation
                };
                if (aFTI.Name == null || aFTI.ForceVector == null || aFTI.Rotation == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }
                return aFTI;

            case "getInfoAboutItem":
                GetInfoAboutItem gIAI = new GetInfoAboutItem()
                {
                    Command = command,
                    Type = MessageType.GetInfoAboutItem,
                    Name = m.Name
                };

                if (gIAI.Name == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }
                return gIAI;

            case "destroyItem":
                DestroyItem dIt = new DestroyItem()
                {
                    Command = command,
                    Type = MessageType.DestroyItem,
                    Name = m.Name
                };
                if (dIt.Name == null)
                {
                    throw new Exception("Incorrect number of arguments");
                }
                return dIt;

            default:
                ErrorMessage em = new ErrorMessage()
                {
                    Command = "BadMessage",
                    Type = MessageType.Error,
                    Message = "Could not understand command"
                };
                return em;
        }
    }
}

[Serializable]
public class DumpMessage : JSONAIMessage
{
    public string Name;
    public Vector2 Position;
    public string Detail;
    public string Sensor;
    public string Speaker;
    public string Text;
    public int Duration;
    public string File;
    public string Effector;
    public string Force;
    public string VerticalForce;
    public string HorizontalForce;
    public string Model;
    public State State { get; set; }
    public ReflexJ.ConditionJ[] ProperConditions;
    public JSONAIMessage[] ProperActions;
    public string[] Conditions;
    public DumpMessage[] Actions;
    public string Comparator;
    public float Value;
    public string StateName;
    public bool Negated;
    public string Reflex;
    public string FilePath;
    public float Mass;
    public int Friction;
    public float Rotation;
    public float Endorphins;
    public int Kinematic;
    public Vector2 ForceVector;
}

[Serializable]
public class ErrorMessage : JSONAIMessage
{
    public string Message;
}

[Serializable]
public class DropItem : JSONAIMessage
{
    public string Name;
    public Vector2 Position;
    public string Detail;

    public DropItem() { }
}

[Serializable]
public class SensorRequest : JSONAIMessage
{
    public string Sensor;

    public SensorRequest() { }
}

[Serializable]
public class Say : JSONAIMessage
{
    public string Speaker;
    public string Text;
    public int Duration;
    public Vector2 Position;

    public Say() { }
}

[Serializable]
public class Print : JSONAIMessage
{
    public string Text;

    public Print() { }
}

[Serializable]
public class LoadTask : JSONAIMessage
{
    public string File;

    public LoadTask() { }
}

[Serializable]
public class FindObj : JSONAIMessage
{
    public string Name;
    public string Model;

    public FindObj() { }
}

[Serializable]
public class AddForce : JSONAIMessage
{
    public string Effector;
    // Needs both force and direction specific
    // so it can handle one input or two
    public fnNode Force;
    public fnNode VerticalForce;
    public fnNode HorizontalForce;

    public AddForce() { }
}

[Serializable]
public class SetState : JSONAIMessage
{
    public string Name;
    public int Duration;
    public State State { get; set; }

    public SetState() { }
}

[Serializable]
public class SetReflex : JSONAIMessage
{
    public string Name;
    public DumpMessage[] Conditions;
    public DumpMessage[] Actions;
    public ReflexJ.ConditionJ[] ProperConditions;
    public JSONAIMessage[] ProperActions;
    public ReflexJ Reflex;
}

[Serializable]
public class ReflexJ
{

    public abstract class ConditionJ
    {

    }
    [Serializable]
    public class SensoryConditionJ : ConditionJ
    {
        public string Sensor;
        public string Comparator;
        public float Value;
    }

    [Serializable]
    public class StateConditionJ : ConditionJ
    {
        public string StateName;
        public bool Negated;
    }


    public string ReflexName { get; set; }
    public List<ConditionJ> Conditions { get; private set; }
    public List<JSONAIMessage> Actions { get; set; }

    public ReflexJ(string name)
    {
        ReflexName = name;
        Conditions = new List<ConditionJ>();
        Actions = new List<JSONAIMessage>();
    }

    public void addCondition(string name, bool negated)
    {
        Conditions.Add(new StateConditionJ()
        {
            StateName = name,
            Negated = negated
        });
    }

    public void addCondition(string sensor, string comparator, float value)
    {
        Conditions.Add(new SensoryConditionJ()
        {
            Sensor = sensor,
            Comparator = comparator,
            Value = value
        });
    }


}

[Serializable]
public class RemoveReflex : JSONAIMessage
{
    public string Name;

    public RemoveReflex() { }
}

[Serializable]
public class GetActiveStates : JSONAIMessage
{
    public GetActiveStates() { }
}

[Serializable]
public class GetActiveReflexes : JSONAIMessage
{
    public GetActiveReflexes() { }
}

[Serializable]
public class CreateItem : JSONAIMessage
{
    public string Name;
    public string FilePath;
    public Vector2 Position;
    public float Mass;
    public int Friction;
    public float Rotation;
    public float Endorphins;
    public int Kinematic;

    public CreateItem() { }
}

[Serializable]
public class AddForceToItem : JSONAIMessage
{
    public string Name;
    public Vector2 ForceVector;
    public float Rotation;
}

[Serializable]
public class GetInfoAboutItem : JSONAIMessage
{
    public string Name;
}

[Serializable]
public class DestroyItem : JSONAIMessage
{
    public string Name;
}
