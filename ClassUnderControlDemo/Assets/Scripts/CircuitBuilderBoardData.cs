using UnityEngine;

public enum CircuitComponentType
{
    Battery,
    Switch,
    Bulb,
    Resistor,
    Fuse,
    Ammeter
}

[System.Serializable]
public class CircuitComponentData
{
    public CircuitComponentType componentType = CircuitComponentType.Battery;
    public string label = "Battery";
    public Sprite icon;
}

[System.Serializable]
public class CircuitSocketData
{
    public string socketLabel = "Socket";
    public CircuitComponentType expectedComponent = CircuitComponentType.Battery;
}

[System.Serializable]
public class CircuitPuzzleData
{
    public string title = "Circuit";
    [TextArea] public string instruction = "Place each component in the correct socket.";
    public CircuitSocketData[] sockets = new CircuitSocketData[0];
    public CircuitComponentData[] components = new CircuitComponentData[0];
}

[System.Serializable]
public class CircuitBuilderClassData
{
    public CircuitPuzzleData[] circuits = new CircuitPuzzleData[0];
}
