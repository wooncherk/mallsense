import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Background, Controls, MiniMap, ReactFlow, ReactFlowProvider, useEdgesState, useNodesState,
  useReactFlow, type Connection, type Edge, type Node,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { CheckCircle2, GripVertical, Paperclip, Pencil, Plus, Send, Save, Sparkles, Trash2, Unlink, X } from 'lucide-react';
import {
  deleteNavigationEdge, deleteNavigationNode, getAllStores, getNavigationEdges, getNavigationNodes,
  generateNavigationAiDraft, saveNavigationEdge, saveNavigationNode, validateNavigationGraph,
} from '../api';
import type { NavigationAiDraft, NavigationAiTurn, NavigationEdge, NavigationGraphValidation, NavigationNode, SaveNavigationEdgeInput, SaveNavigationNodeInput, StoreDto } from '../types';

const nodeTypes = [
  [1, 'Shop'], [2, 'Junction'], [3, 'Escalator'], [4, 'Lift'], [5, 'Entrance'], [6, 'Restroom'],
  [7, 'Surau'], [8, 'Information counter'], [9, 'Landmark'],
] as const;
const nodeName = new Map<number, string>(nodeTypes);
const defaultPalette = [
  { id: 'destination', label: 'Destination', nodeType: 9 },
  { id: 'waypoint', label: 'Waypoint', nodeType: 2 },
  { id: 'entrance', label: 'Entrance / exit', nodeType: 5 },
  { id: 'lift', label: 'Lift', nodeType: 4 },
  { id: 'escalator', label: 'Escalator / stairs', nodeType: 3 },
];
type PaletteItem = { id: string; label: string; nodeType: number };
type FloorDefinition = { number: number; name: string };

const FLOOR_STORAGE_KEY = 'mallsense-navigation-floors';
const MAX_AI_GRAPH_REPAIR_RETRIES = 3;
const CANVAS_SIZE = 1000;

function loadFloorDefinitions(): FloorDefinition[] {
  try {
    const stored = JSON.parse(localStorage.getItem(FLOOR_STORAGE_KEY) || '[]') as FloorDefinition[];
    return stored.filter((item) => Number.isFinite(item.number) && item.name?.trim());
  } catch {
    return [];
  }
}

function GeminiIcon({ size = 16 }: { size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M12 2c0 5.52-4.48 10-10 10 5.52 0 10 4.48 10 10 0-5.52 4.48-10 10-10-5.52 0-10-4.48-10-10Z" fill="currentColor" /></svg>;
}

function NodeBuilderCanvas() {
  const [graphNodes, setGraphNodes] = useState<NavigationNode[]>([]);
  const [graphEdges, setGraphEdges] = useState<NavigationEdge[]>([]);
  const [tenants, setTenants] = useState<StoreDto[]>([]);
  const [flowNodes, setFlowNodes, onNodesChange] = useNodesState<Node>([]);
  const [flowEdges, setFlowEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [floor, setFloor] = useState<number | null>(() => loadFloorDefinitions()[0]?.number ?? null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [validation, setValidation] = useState<NavigationGraphValidation | null>(null);
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [customPalette, setCustomPalette] = useState<PaletteItem[]>(() => {
    try { return JSON.parse(localStorage.getItem('mallsense-custom-node-types') || '[]') as PaletteItem[]; } catch { return []; }
  });
  const [showCustomType, setShowCustomType] = useState(false);
  const [customTypeName, setCustomTypeName] = useState('');
  const [customBehavior, setCustomBehavior] = useState(9);
  const [aiConversation, setAiConversation] = useState<NavigationAiTurn[]>([]);
  const [aiFiles, setAiFiles] = useState<File[]>([]);
  const [aiMessage, setAiMessage] = useState('');
  const [aiDraft, setAiDraft] = useState<NavigationAiDraft | null>(null);
  const [showAiPanel, setShowAiPanel] = useState(true);
  const [floorDefinitions, setFloorDefinitions] = useState<FloorDefinition[]>(loadFloorDefinitions);
  const [floorDialogMode, setFloorDialogMode] = useState<'add' | 'rename' | null>(null);
  const [showDeleteFloor, setShowDeleteFloor] = useState(false);
  const [draggedFloor, setDraggedFloor] = useState<number | null>(null);
  const [floorFormNumber, setFloorFormNumber] = useState(2);
  const [floorFormName, setFloorFormName] = useState('');
  const { screenToFlowPosition, fitView } = useReactFlow();

  const reload = useCallback(async () => {
    const [nodes, edges, stores] = await Promise.all([getNavigationNodes(), getNavigationEdges(), getAllStores()]);
    setGraphNodes(nodes); setGraphEdges(edges); setTenants(stores);
    setFloor((current) => current ?? nodes.map((node) => node.floorNumber).sort((a, b) => a - b)[0] ?? null);
  }, []);

  useEffect(() => { void reload().catch(() => setMessage('Unable to load the navigation graph.')); }, [reload]);
  useEffect(() => {
    const visible = graphNodes.filter((node) => node.floorNumber === floor);
    setFlowNodes(visible.map((node) => ({
      id: node.id,
      position: { x: node.coordinateX / 100 * CANVAS_SIZE, y: node.coordinateY / 100 * CANVAS_SIZE },
      data: { label: node.label },
      style: {
        border: selectedNodeId === node.id ? '2px solid #7c3aed' : '1px solid #cbd5e1',
        borderRadius: 12, padding: 10, fontSize: 12, fontWeight: 700,
        background: node.nodeType === 1 ? '#ecfdf5' : node.nodeType === 5 ? '#fff7ed' : '#fff',
        zIndex: 1,
      },
    })));
    const ids = new Set(visible.map((node) => node.id));
    setFlowEdges(graphEdges.filter((edge) => ids.has(edge.sourceNodeId) && ids.has(edge.targetNodeId)).map((edge) => ({
      id: edge.id, source: edge.sourceNodeId, target: edge.targetNodeId,
      label: `${Math.round(edge.distanceMeters)}m`,
      style: { stroke: selectedEdgeId === edge.id ? '#7c3aed' : '#64748b', strokeWidth: selectedEdgeId === edge.id ? 3 : 2 },
    })));
  }, [graphNodes, graphEdges, floor, selectedNodeId, selectedEdgeId, setFlowNodes, setFlowEdges]);

  const selectedNode = graphNodes.find((node) => node.id === selectedNodeId) || null;
  const selectedEdge = graphEdges.find((edge) => edge.id === selectedEdgeId) || null;
  const floors = useMemo(() => {
    const configured = floorDefinitions.map((item) => item.number);
    const configuredSet = new Set(configured);
    const remaining = graphNodes.map((node) => node.floorNumber)
      .filter((number) => !configuredSet.has(number))
      .filter((number, index, values) => values.indexOf(number) === index)
      .sort((a, b) => a - b);
    if (floor !== null && !configuredSet.has(floor) && !remaining.includes(floor)) remaining.push(floor);
    return [...configured, ...remaining];
  }, [graphNodes, floorDefinitions, floor]);
  const palette = useMemo(() => [...defaultPalette, ...customPalette], [customPalette]);
  useEffect(() => {
    localStorage.setItem(FLOOR_STORAGE_KEY, JSON.stringify(floorDefinitions));
  }, [floorDefinitions]);

  const run = async (action: () => Promise<void>) => {
    setBusy(true); setMessage('');
    try { await action(); } catch (error) { setMessage(error instanceof Error ? error.message : 'Unable to save the graph.'); }
    finally { setBusy(false); }
  };

  const createNode = async (nodeType: number, x: number, y: number, customLabel?: string, useExactLabel = false, targetFloor = floor ?? 1) => {
    const label = useExactLabel ? customLabel!.trim() : `New ${customLabel || nodeName.get(nodeType) || 'location'}`;
    const description = customLabel ? `Node type: ${customLabel}` : '';
    const created = await saveNavigationNode({ floorNumber: targetFloor, label, nodeType, coordinateX: x, coordinateY: y, description, tenantId: null });
    setGraphNodes((current) => [...current, created]); setSelectedNodeId(created.id); setSelectedEdgeId(null);
    setFloor(targetFloor);
  };

  const onDrop = (event: React.DragEvent) => {
    event.preventDefault();
    const raw = event.dataTransfer.getData('application/mallsense-node');
    if (!raw) return;
    const item = JSON.parse(raw) as PaletteItem;
    const position = screenToFlowPosition({ x: event.clientX, y: event.clientY });
    const x = Math.max(0, Math.min(100, position.x / CANVAS_SIZE * 100));
    const y = Math.max(0, Math.min(100, position.y / CANVAS_SIZE * 100));
    void run(() => createNode(item.nodeType, x, y, item.label));
  };

  const addNodeFromPalette = (item: PaletteItem) => {
    if (floor === null) { setMessage('Add a floor before adding locations.'); return; }
    void run(async () => {
      await createNode(item.nodeType, 50, 50, item.label);
      setMessage(`New ${item.label.toLowerCase()} added to ${floorName(floor)}. Drag it to the correct position.`);
      setTimeout(() => void fitView({ padding: .2 }), 30);
    });
  };

  const onNodeDragStop = (_: MouseEvent | TouchEvent, dragged: Node) => {
    const original = graphNodes.find((node) => node.id === dragged.id);
    if (!original) return;
    const input: SaveNavigationNodeInput = {
      floorNumber: original.floorNumber, label: original.label, nodeType: original.nodeType,
      coordinateX: Math.max(0, Math.min(100, dragged.position.x / CANVAS_SIZE * 100)),
      coordinateY: Math.max(0, Math.min(100, dragged.position.y / CANVAS_SIZE * 100)),
      description: original.description, tenantId: original.tenantId,
    };
    void run(async () => {
      const updated = await saveNavigationNode(input, original.id);
      setGraphNodes((current) => current.map((node) => node.id === updated.id ? updated : node));
      setMessage('Position saved.');
    });
  };

  const connectionDefaults = (sourceId: string, targetId: string) => {
    const source = graphNodes.find((node) => node.id === sourceId)!;
    const target = graphNodes.find((node) => node.id === targetId)!;
    const floorChange = target.floorNumber - source.floorNumber;
    const distance = Math.max(1, Math.round(Math.hypot(target.coordinateX - source.coordinateX, target.coordinateY - source.coordinateY)));
    const usesLift = source.nodeType === 4 || target.nodeType === 4;
    const usesEscalator = source.nodeType === 3 || target.nodeType === 3;
    return {
      sourceNodeId: sourceId, targetNodeId: targetId, distanceMeters: distance,
      directionHint: floorChange === 0 ? 'walk_straight' : usesLift ? 'take_lift' : floorChange > 0 ? 'take_escalator_up' : 'take_escalator_down',
      floorChange, isAccessible: !usesEscalator, isBidirectional: true,
    };
  };

  const connect = (connection: Connection) => {
    if (!connection.source || !connection.target) return;
    void run(async () => { await saveNavigationEdge(connectionDefaults(connection.source!, connection.target!)); await reload(); setMessage('Connection saved automatically.'); });
  };

  const saveSelectedNode = (patch: Partial<SaveNavigationNodeInput>) => {
    if (!selectedNode) return;
    const input: SaveNavigationNodeInput = {
      floorNumber: selectedNode.floorNumber, label: selectedNode.label, nodeType: selectedNode.nodeType,
      coordinateX: selectedNode.coordinateX, coordinateY: selectedNode.coordinateY,
      description: selectedNode.description, tenantId: selectedNode.tenantId, ...patch,
    };
    void run(async () => { const updated = await saveNavigationNode(input, selectedNode.id); setGraphNodes((current) => current.map((node) => node.id === updated.id ? updated : node)); setFloor(updated.floorNumber); setMessage('Location details saved.'); });
  };

  const saveSelectedEdge = (patch: Partial<SaveNavigationEdgeInput>) => {
    if (!selectedEdge) return;
    const input: SaveNavigationEdgeInput = {
      sourceNodeId: selectedEdge.sourceNodeId, targetNodeId: selectedEdge.targetNodeId,
      distanceMeters: selectedEdge.distanceMeters, directionHint: selectedEdge.directionHint,
      floorChange: selectedEdge.floorChange, isAccessible: selectedEdge.isAccessible,
      isBidirectional: selectedEdge.isBidirectional, ...patch,
    };
    void run(async () => { const updated = await saveNavigationEdge(input, selectedEdge.id); setGraphEdges((current) => current.map((edge) => edge.id === updated.id ? updated : edge)); setMessage('Connection details saved.'); });
  };

  const removeNode = () => selectedNode && run(async () => { await deleteNavigationNode(selectedNode.id); setSelectedNodeId(null); await reload(); setMessage('Node deleted.'); });
  const removeEdge = () => selectedEdge && run(async () => { await deleteNavigationEdge(selectedEdge.id); setSelectedEdgeId(null); await reload(); setMessage('Connection deleted.'); });
  const validate = () => run(async () => setValidation(await validateNavigationGraph()));

  const connectSelectedNodeToFloor = (targetNodeId: string) => {
    if (!selectedNode) return;
    void run(async () => { await saveNavigationEdge(connectionDefaults(selectedNode.id, targetNodeId)); await reload(); setMessage('Floor transition saved.'); });
  };

  const showNodeOnFloor = (nodeId: string) => {
    const target = graphNodes.find((node) => node.id === nodeId);
    if (!target) return;
    setFloor(target.floorNumber);
    setSelectedNodeId(target.id);
    setSelectedEdgeId(null);
    setTimeout(() => void fitView({ padding: .2, nodes: [{ id: target.id }] }), 30);
  };

  const addCustomType = () => {
    const label = customTypeName.trim();
    if (!label) return;
    const next = [...customPalette, { id: crypto.randomUUID(), label, nodeType: customBehavior }];
    setCustomPalette(next); localStorage.setItem('mallsense-custom-node-types', JSON.stringify(next));
    setCustomTypeName(''); setShowCustomType(false);
  };

  const floorName = (floorNumber: number | null) => floorNumber === null ? 'No floor' : floorDefinitions.find((item) => item.number === floorNumber)?.name || `Level ${floorNumber}`;
  const reorderFloor = (source: number, target: number) => {
    if (source === target) return;
    setFloorDefinitions(() => {
      const ordered = floors.map((number) => ({ number, name: floorName(number) }));
      const sourceIndex = ordered.findIndex((item) => item.number === source);
      const targetIndex = ordered.findIndex((item) => item.number === target);
      if (sourceIndex < 0 || targetIndex < 0) return ordered;
      const [moved] = ordered.splice(sourceIndex, 1);
      ordered.splice(targetIndex, 0, moved);
      return ordered;
    });
  };
  const moveFloorByKeyboard = (item: number, direction: -1 | 1) => {
    const index = floors.indexOf(item);
    const target = floors[index + direction];
    if (target !== undefined) reorderFloor(item, target);
  };
  const openAddFloor = () => {
    const nextNumber = Math.max(0, ...floors) + 1;
    setFloorFormNumber(nextNumber);
    setFloorFormName(`Level ${nextNumber}`);
    setFloorDialogMode('add');
  };
  const openRenameFloor = () => {
    if (floor === null) return;
    setFloorFormNumber(floor);
    setFloorFormName(floorName(floor));
    setFloorDialogMode('rename');
  };
  const saveFloor = () => {
    const name = floorFormName.trim();
    if (!name || !Number.isInteger(floorFormNumber)) return;
    if (floorDialogMode === 'add' && floors.includes(floorFormNumber)) {
      setMessage(`Floor ${floorFormNumber} already exists.`);
      return;
    }
    setFloorDefinitions((current) => {
      const withoutFloor = current.filter((item) => item.number !== floorFormNumber);
      return [...withoutFloor, { number: floorFormNumber, name }].sort((a, b) => a.number - b.number);
    });
    setFloor(floorFormNumber);
    setSelectedNodeId(null);
    setSelectedEdgeId(null);
    setFloorDialogMode(null);
    setMessage(floorDialogMode === 'add' ? `${name} added. Drag a node onto the canvas to begin.` : `Floor renamed to ${name}.`);
    setTimeout(() => void fitView({ padding: .2 }), 20);
  };
  const deleteFloor = () => {
    if (floor === null) return;
    const name = floorName(floor);
    const floorNodes = graphNodes.filter((node) => node.floorNumber === floor);
    const nodeIds = new Set(floorNodes.map((node) => node.id));
    const connectedEdges = graphEdges.filter((edge) => nodeIds.has(edge.sourceNodeId) || nodeIds.has(edge.targetNodeId));
    const remainingFloors = floors.filter((item) => item !== floor);
    const nextFloor = remainingFloors.find((item) => item > floor) ?? remainingFloors.at(-1) ?? null;
    void run(async () => {
      for (const edge of connectedEdges) await deleteNavigationEdge(edge.id);
      for (const node of floorNodes) await deleteNavigationNode(node.id);
      setFloorDefinitions((current) => current.filter((item) => item.number !== floor));
      setSelectedNodeId(null);
      setSelectedEdgeId(null);
      setValidation(null);
      setShowDeleteFloor(false);
      setFloor(nextFloor);
      await reload();
      setMessage(`${name} deleted with ${floorNodes.length} location${floorNodes.length === 1 ? '' : 's'}.`);
      setTimeout(() => void fitView({ padding: .2 }), 30);
    });
  };

  const applyDraftToCanvas = async (
    draft: NavigationAiDraft,
    sourceNodes: NavigationNode[] = graphNodes,
    sourceEdges: NavigationEdge[] = graphEdges,
  ) => {
    const ids = new Map<string, string>();
    const savedNodes = new Map<string, NavigationNode>();
    const normalizeTenantName = (value: string) => value.normalize('NFKD').toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
    const floorLabelKey = (floorNumber: number, label: string) => `${floorNumber}:${label.trim().toLowerCase()}`;
    const floorTenantKey = (floorNumber: number, name: string) => `${floorNumber}:${normalizeTenantName(name)}`;
    const assignedTenantIds = new Set(sourceNodes.map((node) => node.tenantId).filter((id): id is string => Boolean(id)));
    const tenantByFloorAndName = new Map(tenants.map((tenant) => [floorTenantKey(tenant.floorNumber, tenant.name), tenant]));
    for (const existing of sourceNodes) {
      ids.set(existing.id, existing.id);
      ids.set(floorLabelKey(existing.floorNumber, existing.label), existing.id);
      savedNodes.set(existing.id, existing);
    }

    for (const node of draft.nodes) {
      if (!Number.isInteger(node.floorNumber) || node.floorNumber < -10 || node.floorNumber > 200)
        throw new Error(`AI did not assign a valid floor to ${node.label}.`);
      const nodeFloor = node.floorNumber;
      const existingId = ids.get(node.key) || ids.get(floorLabelKey(nodeFloor, node.label));
      const existing = existingId ? sourceNodes.find((item) => item.id === existingId) : undefined;
      const matchingTenant = tenantByFloorAndName.get(floorTenantKey(nodeFloor, node.label));
      const tenantId = existing?.tenantId || (matchingTenant && !assignedTenantIds.has(matchingTenant.id) ? matchingTenant.id : null);
      const input: SaveNavigationNodeInput = {
        floorNumber: nodeFloor, label: node.label, nodeType: aiType(node.type),
        coordinateX: Math.max(0, Math.min(100, node.x)), coordinateY: Math.max(0, Math.min(100, node.y)),
        description: node.description || existing?.description || `AI map type: ${node.type}`,
        tenantId,
      };
      const saved = await saveNavigationNode(input, existing?.id);
      if (saved.tenantId) assignedTenantIds.add(saved.tenantId);
      ids.set(node.key, saved.id);
      ids.set(floorLabelKey(saved.floorNumber, node.label), saved.id);
      savedNodes.set(saved.id, saved);
    }

    for (const edge of draft.edges) {
      const sourceNodeId = ids.get(edge.from) || ids.get(edge.from.trim().toLowerCase());
      const targetNodeId = ids.get(edge.to) || ids.get(edge.to.trim().toLowerCase());
      if (!sourceNodeId || !targetNodeId || sourceNodeId === targetNodeId) continue;
      const existing = sourceEdges.find((item) =>
        (item.sourceNodeId === sourceNodeId && item.targetNodeId === targetNodeId) ||
        (item.isBidirectional && item.sourceNodeId === targetNodeId && item.targetNodeId === sourceNodeId));
      const source = savedNodes.get(sourceNodeId) || sourceNodes.find((item) => item.id === sourceNodeId);
      const target = savedNodes.get(targetNodeId) || sourceNodes.find((item) => item.id === targetNodeId);
      if (!source || !target) continue;
      await saveNavigationEdge({
        sourceNodeId, targetNodeId, distanceMeters: Math.max(1, edge.distanceMeters),
        directionHint: edge.directionHint || 'walk_straight',
        floorChange: target.floorNumber - source.floorNumber,
        isAccessible: edge.accessible, isBidirectional: edge.bidirectional,
      }, existing?.id);
    }
  };

  const askAi = () => run(async () => {
    const operatorMessage = aiMessage.trim() || (aiFiles.length ? `Review ${aiFiles.length} uploaded evidence file(s) and propose a safe draft.` : 'Refine the current draft.');
    const bulkRequest = aiFiles.length > 0 || /whole mall|all floors|entire graph|floor plan|floorplan|complete map/i.test(operatorMessage);
    const targetFloor = selectedNode?.floorNumber ?? graphNodes.find((node) => node.id === selectedEdge?.sourceNodeId)?.floorNumber ?? floor ?? 0;
    const connectionIntent = /\b(connect|connection|link|join|match|pair)\b/i.test(operatorMessage);
    const crossFloorIntent = /\b(cross[- ]floor|different floor|another floor|other floor|between floors|floor to floor|level to level|endpoint|matching endpoint)\b/i.test(operatorMessage)
      || (selectedNode != null && (selectedNode.nodeType === 3 || selectedNode.nodeType === 4) && connectionIntent);
    const relevantNodeIds = new Set<string>();
    if (selectedNode) {
      relevantNodeIds.add(selectedNode.id);
      graphEdges.filter((edge) => edge.sourceNodeId === selectedNode.id || edge.targetNodeId === selectedNode.id)
        .forEach((edge) => { relevantNodeIds.add(edge.sourceNodeId); relevantNodeIds.add(edge.targetNodeId); });
    }
    if (selectedEdge) { relevantNodeIds.add(selectedEdge.sourceNodeId); relevantNodeIds.add(selectedEdge.targetNodeId); }
    if (!bulkRequest && connectionIntent && crossFloorIntent) {
      const selectedEndpointType = selectedNode?.nodeType === 3 || selectedNode?.nodeType === 4 ? selectedNode.nodeType : null;
      const requestedEndpointType = /\blift\b/i.test(operatorMessage) ? 4 : /\b(escalator|stairs?)\b/i.test(operatorMessage) ? 3 : null;
      const endpointTypes = new Set([selectedEndpointType, requestedEndpointType].filter((type): type is 3 | 4 => type != null));
      const otherFloorEndpoints = graphNodes.filter((node) => node.floorNumber !== targetFloor && endpointTypes.has(node.nodeType as 3 | 4));
      otherFloorEndpoints.forEach((node) => {
        relevantNodeIds.add(node.id);
        graphEdges.filter((edge) => edge.sourceNodeId === node.id || edge.targetNodeId === node.id)
          .forEach((edge) => { relevantNodeIds.add(edge.sourceNodeId); relevantNodeIds.add(edge.targetNodeId); });
      });
    }
    const contextNodes = bulkRequest ? graphNodes : graphNodes.filter((node) => relevantNodeIds.size > 0
      ? relevantNodeIds.has(node.id)
      : node.floorNumber === floor);
    const contextNodeIds = new Set(contextNodes.map((node) => node.id));
    const contextEdges = bulkRequest ? graphEdges : graphEdges.filter((edge) =>
      (contextNodeIds.has(edge.sourceNodeId) && contextNodeIds.has(edge.targetNodeId)) &&
      (relevantNodeIds.size === 0 || relevantNodeIds.has(edge.sourceNodeId) || relevantNodeIds.has(edge.targetNodeId)));
    const text = bulkRequest
      ? `${operatorMessage}\n\nThis is a bulk map task. Review the complete multi-floor graph, preserve supported existing locations, and self-check the complete returned graph. Map exact tenant-name and unit-number matches. Do not claim Check graph has passed before apply.`
      : `${operatorMessage}\n\nThis is a targeted map edit. Return only the node(s) and connection(s) that need to be added or changed; do not echo unrelated map data. Use the supplied stable node IDs as keys when referring to existing locations. For a connection change, return both endpoint nodes unchanged as context plus only the changed connection. Do not delete anything unless explicitly requested. Preserve tenant links unless this request explicitly changes a tenant assignment.${crossFloorIntent && connectionIntent ? ' Other-floor endpoint candidates and their immediate route connections are included when available. For a cross-floor link, select a candidate only when its label, description, and route context support the match. If evidence is ambiguous, do not invent a connection; explain the uncertainty. When supported, return the existing endpoint nodes unchanged using their stable IDs and add only the connection between them.' : ''} Do not claim Check graph has passed before apply.`;
    const nextConversation: NavigationAiTurn[] = [...aiConversation, { role: 'user', message: operatorMessage }];
    const evidenceFiles = aiFiles;
    setAiConversation(nextConversation);
    setAiMessage('');
    setAiFiles([]);
    const draft = await generateNavigationAiDraft({
      floorNumber: targetFloor,
      message: text, conversation: nextConversation,
      currentNodes: contextNodes.map((node) => ({
        key: node.id, floorNumber: node.floorNumber, label: node.label, type: nodeName.get(node.nodeType) || 'Landmark',
        x: node.coordinateX, y: node.coordinateY, description: node.description, confidence: 1,
      })),
      currentEdges: contextEdges.map((edge) => ({
        from: edge.sourceNodeId, to: edge.targetNodeId, distanceMeters: edge.distanceMeters,
        accessible: edge.isAccessible, bidirectional: edge.isBidirectional, directionHint: edge.directionHint,
      })),
      files: evidenceFiles,
    });
    setAiDraft(draft);
    setAiConversation([...nextConversation, { role: 'assistant', message: draft.assistantMessage }]);
    setMessage('AI map proposal is ready. Review the targeted changes, then apply them.');
  });

  const applyAiDraft = () => aiDraft && run(async () => {
    let appliedDraft = aiDraft;
    let repairRetries = 0;
    const repairTurns: NavigationAiTurn[] = [];
    await applyDraftToCanvas(appliedDraft);
    let graphCheck = await validateNavigationGraph();

    while ((graphCheck.errors.length || graphCheck.warnings.length) && repairRetries < MAX_AI_GRAPH_REPAIR_RETRIES) {
      repairRetries += 1;
      const findings = [...graphCheck.errors, ...graphCheck.warnings];
      const [latestNodes, latestEdges] = await Promise.all([getNavigationNodes(), getNavigationEdges()]);
      const repairPrompt = `Automatic Check graph retry ${repairRetries} of ${MAX_AI_GRAPH_REPAIR_RETRIES} found:\n${findings.map((finding) => `- ${finding}`).join('\n')}\nPreserve every existing location across all floors. Repair every supported same-floor and cross-floor connection, include every existing node in the returned draft, map exact tenant-name and unit-number matches, and ensure the mall graph has no isolated nodes, dangling links, duplicates, or unsupported omissions.`;
      const retryConversation: NavigationAiTurn[] = [
        ...aiConversation,
        { role: 'assistant', message: appliedDraft.assistantMessage },
        ...repairTurns,
        { role: 'user', message: repairPrompt },
      ];
      appliedDraft = await generateNavigationAiDraft({
        floorNumber: floor ?? 0,
        message: repairPrompt,
        conversation: retryConversation,
        currentNodes: latestNodes.map((node) => ({
          key: node.id, floorNumber: node.floorNumber, label: node.label, type: nodeName.get(node.nodeType) || 'Landmark',
          x: node.coordinateX, y: node.coordinateY, description: node.description, confidence: 1,
        })),
        currentEdges: latestEdges.map((edge) => ({
          from: edge.sourceNodeId, to: edge.targetNodeId, distanceMeters: edge.distanceMeters,
          accessible: edge.isAccessible, bidirectional: edge.isBidirectional, directionHint: edge.directionHint,
        })),
        files: [],
      });
      repairTurns.push({ role: 'user', message: repairPrompt }, { role: 'assistant', message: appliedDraft.assistantMessage });
      await applyDraftToCanvas(appliedDraft, latestNodes, latestEdges);
      graphCheck = await validateNavigationGraph();
    }

    await reload();
    setValidation(graphCheck);
    setAiDraft(null);
    if (graphCheck.errors.length || graphCheck.warnings.length) {
      const findings = [...graphCheck.errors, ...graphCheck.warnings];
      setAiConversation((current) => [...current, ...repairTurns, { role: 'assistant', message: `Stopped after ${repairRetries} automatic repair retries. Check graph still reports ${findings.length} issue${findings.length === 1 ? '' : 's'}; review the visible findings before trying again.` }]);
      setMessage(`AI map repair stopped at the ${MAX_AI_GRAPH_REPAIR_RETRIES}-retry safety limit with ${findings.length} Check graph issue${findings.length === 1 ? '' : 's'} remaining.`);
    } else {
      setAiConversation((current) => [...current, ...repairTurns, { role: 'assistant', message: `Automatic Check graph passed after ${repairRetries} repair ${repairRetries === 1 ? 'retry' : 'retries'}.` }]);
      setMessage(`AI map changes applied across all floors. Automatic Check graph passed${repairRetries ? ` after ${repairRetries} repair ${repairRetries === 1 ? 'retry' : 'retries'}` : ''}.`);
    }
    setTimeout(() => void fitView({ padding: .2 }), 30);
  });

  const aiType = (value: string) => {
    const normalized = value.toLowerCase().replaceAll(' ', '');
    if (normalized.includes('shop')) return 1;
    if (normalized.includes('junction') || normalized.includes('waypoint')) return 2;
    if (normalized.includes('escalator') || normalized.includes('stair')) return 3;
    if (normalized.includes('lift') || normalized.includes('elevator')) return 4;
    if (normalized.includes('entrance') || normalized.includes('exit')) return 5;
    if (normalized.includes('restroom') || normalized.includes('toilet')) return 6;
    if (normalized.includes('surau') || normalized.includes('prayer')) return 7;
    if (normalized.includes('information') || normalized.includes('counter')) return 8;
    return 9;
  };

  return <div className="mx-auto max-w-7xl space-y-5">
    <section className="admin-page-header flex flex-wrap items-end justify-between gap-4 p-6 md:p-8">
      <div><h2 className="text-3xl font-bold tracking-[-0.04em]">Navigation builder</h2><p className="mt-2 max-w-3xl text-sm text-base-content/55">Drag locations onto the floor plan, move them into place, and connect their handles. This private canvas powers shopper instructions.</p></div>
      <button className="btn btn-outline btn-sm" onClick={validate} disabled={busy}><CheckCircle2 size={15} /> Check graph</button>
    </section>
    {message && <div className="alert py-3 text-sm">{message}</div>}
    {validation && <div className={`alert text-sm ${validation.isValid ? 'alert-success' : 'alert-error'}`}><div><strong>{validation.isValid ? 'Graph is ready' : 'Graph needs attention'}</strong><div>{validation.errors.length} errors · {validation.warnings.length} warnings</div>{[...validation.errors, ...validation.warnings].slice(0, 4).map((item) => <div key={item}>• {item}</div>)}</div></div>}

    <div className={`grid items-start gap-5 ${showAiPanel ? 'xl:grid-cols-[22rem_minmax(0,1fr)]' : 'grid-cols-1'}`}>
    <section className="surface-card min-w-0 overflow-hidden xl:order-2">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-base-300 p-4">
        <div className="flex flex-wrap items-center gap-2">
          <button className={`btn btn-sm btn-square rounded-lg ${showAiPanel ? 'btn-secondary' : 'btn-ghost border border-base-300'}`} onClick={() => setShowAiPanel((value) => !value)} aria-label={showAiPanel ? 'Hide AI map assistant' : 'Show AI map assistant'} title="AI map assistant"><Sparkles size={16} /></button>
          {floors.map((item) => <div key={item} className={`join transition-opacity ${draggedFloor === item ? 'opacity-45' : ''}`} onDragOver={(event) => { if (draggedFloor !== null) { event.preventDefault(); event.dataTransfer.dropEffect = 'move'; } }} onDrop={(event) => { event.preventDefault(); const source = Number(event.dataTransfer.getData('application/mallsense-floor')); if (Number.isFinite(source)) reorderFloor(source, item); setDraggedFloor(null); }}>
            <span className="btn btn-ghost btn-sm btn-square join-item cursor-grab border-r-0 px-1 active:cursor-grabbing" draggable aria-label={`Drag to reorder ${floorName(item)}`} title="Drag to reorder floor tabs" role="button" tabIndex={0} onDragStart={(event) => { setDraggedFloor(item); event.dataTransfer.setData('application/mallsense-floor', String(item)); event.dataTransfer.effectAllowed = 'move'; }} onDragEnd={() => setDraggedFloor(null)} onKeyDown={(event) => { if (event.key === 'ArrowLeft') { event.preventDefault(); moveFloorByKeyboard(item, -1); } else if (event.key === 'ArrowRight') { event.preventDefault(); moveFloorByKeyboard(item, 1); } }}><GripVertical size={13} /></span>
            <button className={`btn btn-sm join-item ${floor === item ? 'btn-primary' : 'btn-ghost'}`} onClick={() => { setFloor(item); setSelectedNodeId(null); setSelectedEdgeId(null); setTimeout(() => void fitView({ padding: .2 }), 20); }}>{floorName(item)}</button>{floor === item && <><button className="btn btn-sm btn-square join-item border-l-0" onClick={openRenameFloor} aria-label={`Rename ${floorName(item)}`} title="Rename floor"><Pencil size={13} /></button><button className="btn btn-sm btn-square join-item border-l-0 text-error" disabled={busy} onClick={() => setShowDeleteFloor(true)} aria-label={`Delete ${floorName(item)}`} title="Delete floor"><Trash2 size={13} /></button></>}
          </div>)}
          <button className="btn btn-outline btn-sm btn-square" onClick={openAddFloor} aria-label="Add floor" title="Add floor"><Plus size={16} /></button>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2 border-b border-base-300 bg-base-200/40 px-4 py-3">
        <span className="mr-2 text-xs font-bold uppercase tracking-wide text-base-content/45">Quick add · click or drag onto canvas</span>
        {palette.map((item) => <button key={item.id} draggable={floor !== null} disabled={busy || floor === null} onClick={() => addNodeFromPalette(item)} onDragStart={(event) => { event.dataTransfer.setData('application/mallsense-node', JSON.stringify(item)); event.dataTransfer.effectAllowed = 'copy'; }} className="badge badge-lg cursor-grab border-base-300 bg-base-100 text-xs active:cursor-grabbing"><Plus size={12} /> {item.label}</button>)}
        <button className="btn btn-ghost btn-xs" onClick={() => setShowCustomType((value) => !value)}><Plus size={13} /> Custom type</button>
      </div>

      {showCustomType && <div className="flex flex-wrap items-center gap-2 border-b border-base-300 bg-primary/5 p-3"><span className="text-sm font-bold">New palette type</span><input className="input input-bordered input-sm" value={customTypeName} onChange={(event) => setCustomTypeName(event.target.value)} placeholder="e.g. Art installation" /><select className="select select-bordered select-sm" value={customBehavior} onChange={(event) => setCustomBehavior(Number(event.target.value))}><option value="9">Destination / landmark</option><option value="2">Walkway / waypoint</option><option value="5">Entrance / exit</option><option value="4">Accessible floor connector</option><option value="3">Non-accessible connector</option></select><button className="btn btn-primary btn-sm" disabled={!customTypeName.trim()} onClick={addCustomType}>Add</button></div>}

      <div className={`grid ${(selectedNode || selectedEdge) ? 'xl:grid-cols-[1fr_300px]' : 'grid-cols-1'}`}>
        <div className="relative h-[650px] min-h-[520px]">
          <ReactFlow nodes={flowNodes} edges={flowEdges} onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onNodeDragStop={onNodeDragStop} onConnect={connect} onNodeClick={(_, node) => { setSelectedNodeId(node.id); setSelectedEdgeId(null); }} onEdgeClick={(_, edge) => { setSelectedEdgeId(edge.id); setSelectedNodeId(null); }} onPaneClick={() => { setSelectedNodeId(null); setSelectedEdgeId(null); }} onDrop={onDrop} onDragOver={(event) => { event.preventDefault(); event.dataTransfer.dropEffect = 'copy'; }} fitView fitViewOptions={{ padding: .25 }} snapToGrid snapGrid={[10, 10]} deleteKeyCode={null}>
            <Background gap={20} size={1} color="#cbd5e1" />
            <Controls position="bottom-left" />
            <MiniMap pannable zoomable nodeColor={(node) => node.style?.background as string || '#fff'} />
          </ReactFlow>
          {!flowNodes.length && <div className="pointer-events-none absolute inset-0 grid place-items-center"><div className="rounded-xl border border-dashed border-base-300 bg-base-100/90 px-5 py-4 text-center text-sm text-base-content/55">{floor === null ? <>No floors yet. Use <strong>Add floor</strong> to begin.</> : <>Click or drag a location type from <strong>Quick add</strong> to start {floorName(floor)}.</>}</div></div>}
        </div>

        {(selectedNode || selectedEdge) && <aside className="border-t border-base-300 bg-base-100 p-5 xl:border-l xl:border-t-0">
          {selectedNode ? <NodeEditor node={selectedNode} allNodes={graphNodes} edges={graphEdges} tenants={tenants} busy={busy} onSave={saveSelectedNode} onDelete={removeNode} onConnectFloor={connectSelectedNodeToFloor} onShowNode={showNodeOnFloor} /> : selectedEdge && <EdgeEditor edge={selectedEdge} nodes={graphNodes} busy={busy} onSave={saveSelectedEdge} onDelete={removeEdge} />}
        </aside>}
      </div>
    </section>
    {showAiPanel && <aside className="surface-card flex max-h-[820px] min-h-[650px] flex-col overflow-hidden xl:order-1"><AiGraphAssistant conversation={aiConversation} files={aiFiles} setFiles={setAiFiles} message={aiMessage} setMessage={setAiMessage} draft={aiDraft} busy={busy} ask={askAi} apply={applyAiDraft} beforeNodes={graphNodes.length} beforeEdges={graphEdges.length} onClose={() => setShowAiPanel(false)} /></aside>}
    </div>
    {floorDialogMode && <div className="fixed inset-0 z-[100] grid place-items-center bg-black/40 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-label={floorDialogMode === 'add' ? 'Add floor' : 'Rename floor'} onMouseDown={(event) => { if (event.target === event.currentTarget) setFloorDialogMode(null); }}><div className="w-full max-w-sm rounded-2xl border border-base-300 bg-base-100 p-5 shadow-2xl"><div className="flex items-center justify-between"><div><h3 className="text-lg font-bold">{floorDialogMode === 'add' ? 'Add floor' : 'Rename floor'}</h3><p className="mt-1 text-xs text-base-content/50">{floorDialogMode === 'add' ? 'Create an empty canvas for another mall level.' : `Choose the label shown for floor ${floor}.`}</p></div><button className="btn btn-ghost btn-sm btn-circle" onClick={() => setFloorDialogMode(null)} aria-label="Close"><X size={17} /></button></div><div className="mt-5 grid gap-4">{floorDialogMode === 'add' && <label className="text-xs font-bold text-base-content/55">Floor number<input autoFocus className="input input-bordered mt-1 w-full" type="number" step="1" value={floorFormNumber} onChange={(event) => { const value = Number(event.target.value); setFloorFormNumber(value); if (floorFormName === '' || /^Level -?\d+$/.test(floorFormName)) setFloorFormName(`Level ${value}`); }} /></label>}<label className="text-xs font-bold text-base-content/55">Floor name<input autoFocus={floorDialogMode === 'rename'} className="input input-bordered mt-1 w-full" value={floorFormName} onChange={(event) => setFloorFormName(event.target.value)} placeholder="e.g. Ground Floor" onKeyDown={(event) => { if (event.key === 'Enter') saveFloor(); }} /></label><div className="flex justify-end gap-2 pt-2"><button className="btn btn-ghost btn-sm" onClick={() => setFloorDialogMode(null)}>Cancel</button><button className="btn btn-primary btn-sm whitespace-nowrap" disabled={!floorFormName.trim() || !Number.isInteger(floorFormNumber)} onClick={saveFloor}><Save size={14} /> {floorDialogMode === 'add' ? 'Add floor' : 'Save name'}</button></div></div></div></div>}
    {showDeleteFloor && <div className="fixed inset-0 z-[100] grid place-items-center bg-black/40 p-4 backdrop-blur-sm" role="alertdialog" aria-modal="true" aria-label="Delete floor" onMouseDown={(event) => { if (event.target === event.currentTarget) setShowDeleteFloor(false); }}><div className="w-full max-w-sm rounded-2xl border border-error/30 bg-base-100 p-5 shadow-2xl"><div className="flex items-start gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-full bg-error/10 text-error"><Trash2 size={19} /></span><div><h3 className="text-lg font-bold">Delete {floorName(floor)}?</h3><p className="mt-1 text-sm leading-5 text-base-content/55">This permanently removes {graphNodes.filter((node) => node.floorNumber === floor).length} location{graphNodes.filter((node) => node.floorNumber === floor).length === 1 ? '' : 's'} and all connected routes.</p></div></div><div className="mt-5 flex justify-end gap-2"><button className="btn btn-ghost" disabled={busy} onClick={() => setShowDeleteFloor(false)}>Cancel</button><button className="btn btn-error" disabled={busy} onClick={deleteFloor}>{busy ? <span className="loading loading-spinner loading-xs" /> : <Trash2 size={14} />} Delete floor</button></div></div></div>}
  </div>;
}

function AiGraphAssistant({ conversation, files, setFiles, message, setMessage, draft, busy, ask, apply, beforeNodes, beforeEdges, onClose }: {
  conversation: NavigationAiTurn[]; files: File[]; setFiles: (files: File[]) => void;
  message: string; setMessage: (value: string) => void; draft: NavigationAiDraft | null;
  busy: boolean; ask: () => void; apply: () => void; beforeNodes: number; beforeEdges: number; onClose: () => void;
}) {
  const canSend = !busy && (message.trim().length > 0 || files.length > 0);
  return <div className="flex min-h-0 flex-1 flex-col">
    <div className="border-b border-base-300 px-5 py-4"><div className="flex items-center gap-2"><span className="grid size-8 place-items-center rounded-lg bg-secondary/10 text-secondary"><GeminiIcon size={17} /></span><div className="min-w-0 flex-1"><h3 className="text-sm font-bold">AI map assistant</h3></div><button className="btn btn-ghost btn-sm btn-circle shrink-0" onClick={onClose} aria-label="Close AI build panel" title="Close AI build"><X size={16} /></button></div></div>

    <div className="min-h-0 flex-1 space-y-3 overflow-y-auto bg-base-200/25 p-4">
      {conversation.map((turn, index) => <div key={`${turn.role}-${index}`} className={`rounded-xl px-3 py-2 text-xs leading-5 ${turn.role === 'user' ? 'ml-5 bg-secondary text-secondary-content' : 'mr-5 bg-base-200 text-base-content/70'}`}>{turn.message}</div>)}
      {busy && <div className="mr-5 rounded-xl border border-base-300 bg-base-100 p-3"><div className="flex items-center gap-2"><span className="loading loading-spinner loading-sm text-secondary" /><div><div className="text-xs font-bold">Preparing map proposal</div><p className="mt-1 text-[11px] text-base-content/45">Reviewing your request and current map…</p></div></div></div>}
      {draft && <div className="rounded-xl border border-primary/20 bg-primary/5 p-4 text-xs"><div className="flex items-center justify-between gap-3"><div className="font-bold">Proposed map update</div><span className="badge badge-sm">{draft.confidencePercentage}% confidence</span></div><p className="mt-2 text-base-content/65">{draft.assistantMessage}</p><div className="mt-3 rounded-lg border border-primary/20 bg-base-100 p-3"><div className="font-bold text-primary">Proposed actions</div>{draft.actions.length ? <ul className="mt-2 list-inside list-disc space-y-1">{draft.actions.map((action, index) => <li key={`${action}-${index}`}>{action}</li>)}</ul> : <div className="mt-2 text-base-content/50">No map changes proposed.</div>}</div><div className="mt-2 text-[10px] text-base-content/45">{beforeNodes} locations and {beforeEdges} connections currently on the map · {draft.nodes.length} locations and {draft.edges.length} connections in this proposal</div>{draft.warnings.slice(0, 3).map((warning) => <div key={warning} className="mt-2 text-warning">• {warning}</div>)}<button className="btn btn-primary btn-sm mt-4 w-full" disabled={busy || (draft.nodes.length === 0 && draft.edges.length === 0)} onClick={apply}><Sparkles size={14} /> Apply actions</button><p className="mt-2 text-center text-[10px] text-base-content/40">The map changes only after you apply this proposal.</p></div>}
    </div>

    <div className="shrink-0 border-t border-base-300 bg-base-100 p-4">
      <div className="mb-3 flex flex-wrap gap-1.5">{['Add missing routes', 'Improve accessibility', 'Check all floors'].map((prompt) => <button key={prompt} type="button" className="rounded-full border border-base-300 bg-base-100 px-2.5 py-1 text-[11px] font-medium text-base-content/65 transition-colors hover:bg-base-200" onClick={() => setMessage(prompt)}>{prompt}</button>)}</div>
      {files.length > 0 && <div className="mb-2 rounded-lg bg-base-200 px-3 py-2 text-[11px]"><div className="flex items-center gap-2"><Paperclip size={12} /><span className="min-w-0 flex-1 truncate">{files.length} attachment{files.length === 1 ? '' : 's'} ready</span><button className="btn btn-ghost btn-xs btn-circle" onClick={() => setFiles([])} aria-label="Clear attachments"><X size={12} /></button></div></div>}
      <div className="rounded-xl border border-base-300 bg-base-100 p-2 shadow-sm focus-within:border-secondary/50"><textarea className="min-h-20 w-full resize-none bg-transparent px-1 text-sm leading-5 outline-none placeholder:text-base-content/35" value={message} onChange={(event) => setMessage(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter' && (event.ctrlKey || event.metaKey) && canSend) { event.preventDefault(); ask(); } }} placeholder="What should AI add or correct?" /><div className="mt-1 flex items-center gap-2"><label className="grid size-8 cursor-pointer place-items-center rounded-lg text-base-content/50 hover:bg-base-200" title="Attach evidence"><Paperclip size={15} /><input hidden multiple type="file" accept="image/png,image/jpeg,image/webp,image/gif,application/pdf" onChange={(event) => setFiles(Array.from(event.target.files || []).slice(0, 8))} /></label><span className="mr-auto text-[10px] text-base-content/35">Ctrl + Enter</span><button className="grid size-8 place-items-center rounded-lg bg-secondary text-secondary-content transition-transform hover:scale-105 disabled:cursor-not-allowed disabled:opacity-35" disabled={!canSend} onClick={ask} aria-label={draft ? 'Regenerate proposal' : 'Generate proposal'}>{busy ? <span className="loading loading-spinner loading-xs" /> : <Send size={14} />}</button></div></div>
    </div>
  </div>;
}

function NodeEditor({ node, allNodes, edges, tenants, busy, onSave, onDelete, onConnectFloor, onShowNode }: { node: NavigationNode; allNodes: NavigationNode[]; edges: NavigationEdge[]; tenants: StoreDto[]; busy: boolean; onSave: (patch: Partial<SaveNavigationNodeInput>) => void; onDelete: () => void; onConnectFloor: (targetNodeId: string) => void; onShowNode: (nodeId: string) => void }) {
  const [label, setLabel] = useState(node.label);
  const [nodeFloor, setNodeFloor] = useState(node.floorNumber);
  const [description, setDescription] = useState(node.description || '');
  const [type, setType] = useState(node.nodeType);
  const [tenantId, setTenantId] = useState(node.tenantId || '');
  const [transitionTarget, setTransitionTarget] = useState('');
  const connectedIds = new Set(edges.flatMap((edge) => edge.sourceNodeId === node.id ? [edge.targetNodeId] : edge.targetNodeId === node.id ? [edge.sourceNodeId] : []));
  const linkedEndpoints = edges.filter((edge) => edge.sourceNodeId === node.id || edge.targetNodeId === node.id)
    .map((edge) => allNodes.find((candidate) => candidate.id === (edge.sourceNodeId === node.id ? edge.targetNodeId : edge.sourceNodeId)))
    .filter((candidate): candidate is NavigationNode => Boolean(candidate && candidate.floorNumber !== node.floorNumber));
  const transitionTargets = allNodes.filter((candidate) => candidate.id !== node.id && candidate.floorNumber !== node.floorNumber && candidate.nodeType === node.nodeType && !connectedIds.has(candidate.id));
  useEffect(() => { setLabel(node.label); setNodeFloor(node.floorNumber); setDescription(node.description || ''); setType(node.nodeType); setTenantId(node.tenantId || ''); setTransitionTarget(''); }, [node]);
  return <div><h3 className="flex items-center gap-2 font-bold"><Pencil size={15} /> Edit location</h3><p className="mt-1 text-xs text-base-content/45">Change its name, level, type, tenant, or visitor description.</p><div className="mt-4 grid gap-3"><label className="text-xs font-bold text-base-content/50">Location name<input className="input input-bordered mt-1 w-full" value={label} onChange={(event) => setLabel(event.target.value)} placeholder="Location name" /></label><label className="text-xs font-bold text-base-content/50">Level<input className="input input-bordered mt-1 w-full" type="number" value={nodeFloor} onChange={(event) => setNodeFloor(Number(event.target.value))} /></label><label className="text-xs font-bold text-base-content/50">Routing behaviour<select className="select select-bordered mt-1 w-full" value={type} onChange={(event) => setType(Number(event.target.value))}>{nodeTypes.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label><select className="select select-bordered" value={tenantId} onChange={(event) => setTenantId(event.target.value)}><option value="">No tenant</option>{tenants.map((tenant) => <option key={tenant.id} value={tenant.id}>{tenant.name} · {tenant.unitNumber}</option>)}</select><textarea className="textarea textarea-bordered min-h-24" value={description} onChange={(event) => setDescription(event.target.value)} placeholder="What can shoppers see here?" /><button className="btn btn-primary" disabled={busy || !label.trim() || !Number.isFinite(nodeFloor)} onClick={() => onSave({ label, floorNumber: nodeFloor, description, nodeType: type, tenantId: tenantId || null })}><Save size={14} /> Save changes</button>
    {(node.nodeType === 3 || node.nodeType === 4) && <div className="rounded-xl border border-base-300 p-3"><div className="text-sm font-bold">Connect to another floor</div><p className="mt-1 text-xs text-base-content/50">Link this {node.nodeType === 4 ? 'lift' : 'escalator/stair'} endpoint to the matching endpoint on a different level.</p>{linkedEndpoints.length > 0 && <div className="mt-3"><div className="mb-1 text-xs font-semibold text-success">Already linked</div><div className="grid gap-1">{linkedEndpoints.map((endpoint) => <button key={endpoint.id} type="button" className="btn btn-ghost btn-xs justify-start text-left" onClick={() => onShowNode(endpoint.id)}>Level {endpoint.floorNumber} · {endpoint.label} <span className="ml-auto text-[10px] opacity-60">View</span></button>)}</div></div>}{transitionTargets.length ? <div className="mt-3 grid gap-2"><select className="select select-bordered select-sm w-full" value={transitionTarget} onChange={(event) => setTransitionTarget(event.target.value)}><option value="">Choose matching endpoint</option>{transitionTargets.map((candidate) => <option key={candidate.id} value={candidate.id}>Level {candidate.floorNumber} · {candidate.label}</option>)}</select><button className="btn btn-warning btn-sm" disabled={!transitionTarget || busy} onClick={() => onConnectFloor(transitionTarget)}>Connect floors</button></div> : linkedEndpoints.length === 0 ? <p className="mt-3 text-xs text-warning">Create a matching {node.nodeType === 4 ? 'lift' : 'escalator/stair'} node on another floor first.</p> : null}</div>}
    <button className="btn btn-error btn-outline btn-sm" onClick={onDelete}><Trash2 size={14} /> Remove location</button></div></div>;
}

function EdgeEditor({ edge, nodes, busy, onSave, onDelete }: { edge: NavigationEdge; nodes: NavigationNode[]; busy: boolean; onSave: (patch: Partial<SaveNavigationEdgeInput>) => void; onDelete: () => void }) {
  const [distance, setDistance] = useState(edge.distanceMeters);
  const [direction, setDirection] = useState(edge.directionHint);
  const [accessible, setAccessible] = useState(edge.isAccessible);
  const [bidirectional, setBidirectional] = useState(edge.isBidirectional);
  useEffect(() => { setDistance(edge.distanceMeters); setDirection(edge.directionHint); setAccessible(edge.isAccessible); setBidirectional(edge.isBidirectional); }, [edge]);
  return <div><h3 className="flex items-center gap-2 font-bold"><Pencil size={15} /> Edit connection</h3><p className="mt-2 text-sm text-base-content/55">{nodes.find((node) => node.id === edge.sourceNodeId)?.label} → {nodes.find((node) => node.id === edge.targetNodeId)?.label}</p><div className="mt-4 grid gap-3"><label className="text-xs font-bold text-base-content/50">Distance in metres<input className="input input-bordered mt-1 w-full" type="number" min="0.1" step="0.1" value={distance} onChange={(event) => setDistance(Number(event.target.value))} /></label><label className="text-xs font-bold text-base-content/50">Direction hint<input className="input input-bordered mt-1 w-full" value={direction} onChange={(event) => setDirection(event.target.value)} placeholder="e.g. east or turn_left" /></label><label className="flex items-center gap-3 rounded-xl border border-base-300 p-3 text-sm font-semibold"><input className="checkbox checkbox-primary checkbox-sm" type="checkbox" checked={accessible} onChange={(event) => setAccessible(event.target.checked)} /> Accessible route</label><label className="flex items-center gap-3 rounded-xl border border-base-300 p-3 text-sm font-semibold"><input className="checkbox checkbox-primary checkbox-sm" type="checkbox" checked={bidirectional} onChange={(event) => setBidirectional(event.target.checked)} /> Travel both directions</label><button className="btn btn-primary" disabled={busy || !Number.isFinite(distance) || distance <= 0 || !direction.trim()} onClick={() => onSave({ distanceMeters: distance, directionHint: direction, isAccessible: accessible, isBidirectional: bidirectional })}><Save size={14} /> Save connection</button><button className="btn btn-error btn-outline btn-sm" onClick={onDelete}><Unlink size={14} /> Remove connection</button></div></div>;
}

export function NodeBuilder() {
  return <ReactFlowProvider><NodeBuilderCanvas /></ReactFlowProvider>;
}
