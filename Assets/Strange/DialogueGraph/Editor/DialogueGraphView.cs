﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueGraphView : GraphView
{
    const float StartNodePosX = 100;
    const float StartNodePosY = 200;
    const float StartNodeWidth = 100;
    const float StartNodeHeight= 150;
    public  Vector2 defaultNodeSize = new Vector2(150, 200);
    readonly Vector2 defaultNodePos = new Vector2(100, 100);

    public DialogueGraphView()
    {
        styleSheets.Add(Resources.Load<StyleSheet>("DialogueGraph"));

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale); // allows zooming of graph

        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        GridBackground grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        AddElement( GenerateEntryPointNode());
    }

    private Port GeneratePort(DialogueNode node, Direction portDirection, Port.Capacity capacity = Port.Capacity.Single)
    {
        return node.InstantiatePort(Orientation.Horizontal, portDirection, capacity, typeof(float));
    }

    public DialogueNode GenerateEntryPointNode()
    {
        // Generate a blank node
        DialogueNode node = new DialogueNode 
        {
            title = "START", // name of the node
            GUID = Guid.NewGuid().ToString(), // ID of the node
            DialogueText = "ENTRYPOINT", 
            entrypoint = true
        };

        // generate an output port
        Port port = GeneratePort(node, Direction.Output); 
        port.portName = "next"; // set the title of this port
        node.outputContainer.Add(port); // add the port to the node

        node.capabilities &= ~Capabilities.Deletable;
        node.capabilities &= ~Capabilities.Movable;

        // update the node (also removed the input section if not being used)
        node.RefreshExpandedState();
        node.RefreshPorts();

        node.SetPosition(new Rect(StartNodePosX, StartNodePosY, StartNodeWidth, StartNodeHeight));
        return node;
    }

    public void GenerateEntryPointNode(string customID = "")
    {
        // Generate a blank node
        DialogueNode node = new DialogueNode
        {
            title = "START", // name of the node
            GUID = customID, // ID of the node
            DialogueText = "ENTRYPOINT",
            entrypoint = true
        };

        // generate an output port
        Port port = GeneratePort(node, Direction.Output);
        port.portName = "next"; // set the title of this port
        node.outputContainer.Add(port); // add the port to the node

        // update the node (also removed the input section if not being used)
        node.RefreshExpandedState();
        node.RefreshPorts();

        node.SetPosition(new Rect(StartNodePosX, StartNodePosY, StartNodeWidth, StartNodeHeight));
        AddElement(node);
    }

    public void CreateNode(string nodename)
    {
        AddElement(CreateDialogueNode(nodename));
    }

    private  DialogueNode CreateDialogueNode(string nodeName)
    {
        DialogueNode newNode = new DialogueNode
        {
            title = nodeName,
            DialogueText = nodeName,
            GUID = Guid.NewGuid().ToString()
        };

        Port inputPort = GeneratePort(newNode, Direction.Input, Port.Capacity.Multi);
        inputPort.portName = "input";
        newNode.inputContainer.Add(inputPort);



        //add a text field so the user can change the dialogue text
        TextField textField = new TextField(string.Empty);
        //when the value of textfield is changed, run the below block
        textField.RegisterValueChangedCallback(evt =>
        {
            // update the title of the node
            newNode.title = evt.newValue;
            // change the text stored within the node
            newNode.DialogueText = evt.newValue;
        });
        textField.SetValueWithoutNotify(newNode.title);
        newNode.extensionContainer.Add(textField);



        Button newChoiceButton = new Button(() => AddChoicePort(newNode));
        newChoiceButton.text = "Add Output";
        newNode.titleContainer.Add(newChoiceButton);

        // refresh whenever changes are made
        newNode.RefreshExpandedState();
        newNode.RefreshPorts();

        // set position of new node
        newNode.SetPosition(new Rect(defaultNodePos, defaultNodeSize));

        return newNode;
    }

    private void AddChoicePort(DialogueNode dialogueNode, string portName = "")
    {
        Port outputPort = GeneratePort(dialogueNode, Direction.Output);

        var oldlabel = outputPort.contentContainer.Q<Label>("type");
        outputPort.contentContainer.Remove(oldlabel);

        int outputCount = dialogueNode.outputContainer.Query("connector").ToList().Count;
        if (portName == "")
            portName = "Output " + outputCount;
        outputPort.portName = portName;
        dialogueNode.outputContainer.Add(outputPort);

        TextField textField = new TextField
        {
            name = string.Empty,
            value = portName
        };
        textField.RegisterValueChangedCallback(evt => outputPort.portName = evt.newValue);
        outputPort.contentContainer.Add(new Label("  "));
        outputPort.contentContainer.Add(textField);

        Button deleteButton = new Button(() => RemovePort(dialogueNode, outputPort))
        {
            text = "X"
        };
        outputPort.contentContainer.Add(deleteButton);

        dialogueNode.RefreshExpandedState();
        dialogueNode.RefreshPorts();
    }

    private void RemovePort(DialogueNode dialogueNode, Port outputPort)
    {

        // before we can remove a port, we must first remove all the connections it has
        var targetEdge = edges.ToList().Where(x => x.output.portName == outputPort.portName && x.output.node == outputPort.node);
        if (targetEdge.Any())
        {
            Debug.LogError("if");
            Edge edge = targetEdge.First();
            edge.input.Disconnect(edge);
            RemoveElement(edge);
        }

        // once the edjes are all removed, the port can be removed
        dialogueNode.outputContainer.Remove(outputPort);

        dialogueNode.RefreshPorts();
        dialogueNode.RefreshExpandedState();
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        //return base.GetCompatiblePorts(startPort, nodeAdapter);
        List<Port> compatibleports = new List<Port>();

        ports.ForEach((port) => // fancy foreach statement
        {
            // startport = port at start of connection
            //      port = port at end of connection 

            if (startPort != port) // make sure a port doesn't go into itself (is this necessary?)
            {
                if (startPort.node != port.node) // make sure a node can not call itself
                {
                    compatibleports.Add(port);
                }
            }

        });

        return compatibleports;
    }

    /////////////////////////////////////////////////////
    //                SAVE AND LOAD                    //
    /////////////////////////////////////////////////////

    public void Save(string filename)
    {

        List<NodeSaveState> graphNodes = new List<NodeSaveState>();
        List<EdgeSaveState> graphEdges = new List<EdgeSaveState>();

        nodes.ForEach((node) => // the fancy way of saying 'foreach(var node in nodes)' but for UQueryStates
        {
            // get node data and store it
            NodeSaveState tempNode = new NodeSaveState();
            tempNode.nodePos = node.GetPosition();
            tempNode.isEntryPoint = (node as DialogueNode).entrypoint;
            tempNode.dialogueText = (node as DialogueNode).DialogueText;
            tempNode.guid = (node as DialogueNode).GUID;

            // add the node to the list that is saved
            graphNodes.Add(tempNode);
        });

        edges.ForEach((edge) =>
        {
            // get edge data and store it
            EdgeSaveState tempEdge = new EdgeSaveState();
            tempEdge.outputID = (edge.output.node as DialogueNode).GUID;
            tempEdge.inputID = (edge.input.node as DialogueNode).GUID;
            tempEdge.portName = edge.output.portName;

            // add the edge to the list that is saved
            graphEdges.Add(tempEdge);
        });



        // create an instance of the DialogueGraphSave scriptableObject
        DialogueTree save = ScriptableObject.CreateInstance<DialogueTree>();
        // set the data to the nodes and edges we have just gathered
        save.nodes = graphNodes.ToArray();
        save.connections = graphEdges.ToArray();

        // if a folder does not exist, create one
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // save the scriptableObject to a file
        AssetDatabase.CreateAsset(save, $"Assets/Resources/{filename}.asset");
        AssetDatabase.SaveAssets();



    }
    public void Load(string filename)
    {
        ClearGraph();

        // load the file from the resources folder
        DialogueTree save = Resources.Load<DialogueTree>(filename);


        // GENERATE NODES
        foreach(NodeSaveState node in save.nodes)
        {
            if (node.isEntryPoint)
            {
                GenerateEntryPointNode(node.guid);
            }
            else
            {
                // create a node and set the GUID so match the one we are loading from the file
                DialogueNode tempNode = CreateDialogueNode(node.dialogueText);
                tempNode.GUID = node.guid;
                AddElement(tempNode);

                // get a list of all the connections that connect to this node's outputs
                //   and add a port with the name that belongs to the connection
                List<EdgeSaveState> nodePorts = save.connections.Where(x => x.outputID == node.guid).ToList();
                nodePorts.ForEach(x => AddChoicePort(tempNode, x.portName));

                tempNode.SetPosition(node.nodePos);
            }
        }

        // CONNECT NODES

        List<Node> listOfNodes = nodes.ToList();
        // for each node
        for (int i = 0; i < listOfNodes.Count; i++)
        {
            int k = i;
            // find edges that are outputted by this node
            List<EdgeSaveState> connections = save.connections.Where(x => x.outputID == (listOfNodes[k] as DialogueNode).GUID).ToList();
            for (int j = 0; j < connections.Count(); j++)
            {
                string targetNodeGUID = connections[j].inputID;
                Node targetNode = listOfNodes.First(x => (x as DialogueNode).GUID == targetNodeGUID);

                LinkNodesTogether(listOfNodes[i].outputContainer[j].Q<Port>(), (Port)targetNode.inputContainer[0]);

            }

        }

    }
    public void ClearGraph()
    {
        // for all the nodes on the graph
        nodes.ForEach((node) =>
        {
            if (!(node as DialogueNode).entrypoint)// remove all nodes except the entyrypoint
            {
                // remove all edges on the node first
                edges.ToList().Where(x => x.input.node == node).ToList()
                    .ForEach(edge => RemoveElement(edge));
                // then remove the node
                RemoveElement(node);
            }

        });
    }

    /// <summary>
    /// used when loading in dialogue trees
    /// procedurally conencts two nodes with an edge
    /// </summary>
    /// <param name="outputSocket"> the port coming out of a node</param>
    /// <param name="inputSocket"> the port going in to a node</param>
    private void LinkNodesTogether(Port outputSocket, Port inputSocket)
    {
        var tempEdge = new Edge()
        {
            output = outputSocket,
            input = inputSocket
        };
        tempEdge?.input.Connect(tempEdge);
        tempEdge?.output.Connect(tempEdge);
        Add(tempEdge);
    }

}
