const state = {
  status: null,
  appUpdate: {
    status: null,
    pollHandle: 0
  },
  modding: {
    categories: [],
    assets: [],
    total: 0,
    page: 1,
    pageSize: 40,
    selectedCategoryId: "",
    selectedAssetId: "",
    selectedAsset: null,
    currentSchema: null,
    currentFieldValues: new Map(),
    currentFieldDisplayValues: new Map(),
    currentOriginalValues: new Map(),
    currentListEdits: [],
    currentScene: null,
    currentSceneSelectionId: "",
    currentSceneDrag: null,
    currentSceneFilterKind: "all",
    currentSceneSearch: "",
    currentSceneFocusMode: "all",
    currentSceneNudgeStep: 25,
    schemaLoadToken: 0,
    stagedByAssetId: new Map(),
    showOnlyEditable: false,
    schemaFieldFilter: "",
    customVisualModels: [],
    rawModelImports: [],
    vehicleProfile: null,
    vehicleProfileAssetId: "",
    vehicleProfileLoading: false,
    vehicleModulePlan: null,
    vehicleModulePlanKey: "",
    vehicleModulePlanLoading: false,
    vehicleModulePlanCookingKey: "",
    vehicleModulePlanBatchCooking: false,
    vehicleFullReplacementCooking: false,
    armorSetPlan: null,
    armorSetPlanKey: "",
    armorSetPlanLoading: false,
    armorSetPlanCookingKey: "",
    armorSetPlanBatchCooking: false,
    modelTargetLongestTouched: false,
    modelProfilePreset: "auto"
  }
};

let modAssetSearchDebounce = 0;
let modelMaterialSearchDebounce = 0;
let modelMaterialOptionsToken = 0;
const VEHICLE_ADAPTER_CLIENT_VISIBLE = false;
const SCENE_VIEWBOX_WIDTH = 1000;
const SCENE_VIEWBOX_HEIGHT = 620;
const SCENE_VIEWBOX_PADDING = 72;
const SUPPORT_CARD_NUMBER = "2202 2068 7570 5381";
const SUPPORT_CARD_COMPACT = SUPPORT_CARD_NUMBER.replace(/\s+/g, "");
const SBER_ONLINE_URL = "https://online.sberbank.ru/";

function el(id) {
  return document.getElementById(id);
}

function formatUpdateTime(isoValue) {
  const raw = String(isoValue || "").trim();
  if (!raw) {
    return "";
  }

  const date = new Date(raw);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return date.toLocaleString("ru-RU");
}

function releaseNotesToPlainText(markdown) {
  const raw = String(markdown || "").trim();
  if (!raw) {
    return "";
  }

  const collapsed = raw
    .replace(/```[\s\S]*?```/g, " ")
    .replace(/`([^`]+)`/g, "$1")
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, "$1")
    .replace(/[>#*_~-]+/g, " ")
    .replace(/\r/g, " ")
    .replace(/\n+/g, " ")
    .replace(/\s+/g, " ")
    .trim();

  if (!collapsed) {
    return "";
  }

  return collapsed.length > 260
    ? `${collapsed.slice(0, 257).trim()}...`
    : collapsed;
}

function toIntSafe(value, fallback = 1) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function toNumberSafe(value, fallback = 0) {
  const parsed = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : fallback;
}

function formatSceneFieldValue(value) {
  const rounded = Math.round(toNumberSafe(value, 0) * 1000) / 1000;
  return Number.isFinite(rounded) ? String(rounded) : "0";
}

function formatSceneNumber(value) {
  const rounded = Math.round(toNumberSafe(value, 0) * 100) / 100;
  return Number.isFinite(rounded) ? rounded.toLocaleString("ru-RU") : "0";
}

function timestampNow() {
  const now = new Date();
  const yyyy = now.getFullYear();
  const mm = String(now.getMonth() + 1).padStart(2, "0");
  const dd = String(now.getDate()).padStart(2, "0");
  const hh = String(now.getHours()).padStart(2, "0");
  const min = String(now.getMinutes()).padStart(2, "0");
  const ss = String(now.getSeconds()).padStart(2, "0");
  return `${yyyy}${mm}${dd}-${hh}${min}${ss}`;
}

function setDefaultModName() {
  const input = el("modNameInput");
  if (!input) {
    return;
  }

  input.placeholder = "Оставь пустым для автоматического имени";
  if (/^pakchunk99-scum-studio-\d{8}-\d{6}-windowsnoeditor$/i.test(input.value.trim())) {
    input.value = "";
  }
}

function setSupportStatus(text) {
  const status = el("supportCopyStatus");
  if (status) {
    status.textContent = text || "";
  }
}

function openSupportModal() {
  const modal = el("supportModal");
  if (!modal) {
    return;
  }

  setSupportStatus("");
  modal.hidden = false;
}

function closeSupportModal() {
  const modal = el("supportModal");
  if (!modal) {
    return;
  }

  modal.hidden = true;
}

async function copySupportCard() {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(SUPPORT_CARD_COMPACT);
  } else {
    const input = document.createElement("input");
    input.value = SUPPORT_CARD_COMPACT;
    input.setAttribute("readonly", "readonly");
    input.style.position = "fixed";
    input.style.left = "-9999px";
    document.body.appendChild(input);
    input.select();
    document.execCommand("copy");
    input.remove();
  }

  setSupportStatus("Номер карты скопирован.");
}

function openSberOnline() {
  window.open(SBER_ONLINE_URL, "_blank", "noopener,noreferrer");
}

function formatSourceMode(mode) {
  return {
    auto: "авто",
    preset: "встроенный шаблон",
    game: "файл игры"
  }[mode] || "авто";
}

function formatCompanionMode(mode) {
  return {
    auto: "авто",
    force: "всегда добавлять",
    none: "не добавлять"
  }[mode] || "авто";
}

function formatAssetFormat(format) {
  return {
    uasset: "игровой ассет",
    json: "таблица JSON",
    ini: "конфиг INI",
    csv: "таблица CSV",
    txt: "текстовый файл"
  }[String(format || "").toLowerCase()] || String(format || "файл");
}

function formatListAction(op) {
  const rawValue = String(op.rawValue || "").trim();
  const readableRef = String(op.rawLabel || "").trim() || referenceValueToReadableName(rawValue);
  return {
    "add-clone": `добавить ещё один похожий элемент на основе №${(Number(op.sourceIndex) || 0) + 1}`,
    "add-empty": "добавить новый пустой элемент",
    "add-reference": isSideEffectReferenceValue(rawValue)
      ? `добавить новое последствие: ${readableRef}`
      : `добавить новую связь: ${readableRef}`,
    "remove-index": `убрать элемент №${(Number(op.index) || 0) + 1}`,
    clear: "очистить весь состав"
  }[op.action] || "изменить состав системы";
}

function softObjectToReadableName(value) {
  const raw = String(value || "").trim();
  if (!raw) {
    return "не выбрано";
  }

  const lastDot = raw.lastIndexOf(".");
  let stem = lastDot >= 0 ? raw.slice(lastDot + 1) : raw.split("/").pop() || raw;
  stem = stem.replace(/_C$/i, "");
  return stem
    .replace(/_/g, " ")
    .replace(/([a-zа-я])([A-ZА-Я])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
}

function referenceValueToReadableName(value) {
  const raw = String(value || "").trim();
  if (!raw) {
    return "не выбрано";
  }

  if (raw.toLowerCase().startsWith("script:")) {
    return softObjectToReadableName(raw.slice("script:".length));
  }

  return softObjectToReadableName(raw);
}

function isSideEffectReferenceValue(value) {
  return String(value || "").trim().toLowerCase().startsWith("script:prisonerbodyconditionorsymptomsideeffect_");
}

function getCurrentFieldDisplayValue(field, currentValue) {
  return state.modding.currentFieldDisplayValues.get(field.fieldPath)
    || field.currentDisplayValue
    || referenceValueToReadableName(currentValue);
}

function buildItemClassRef(item) {
  const rel = String(item?.relativePath || "");
  const normalized = rel.replace(/^scum\/content\/conz_files\//i, "");
  const withoutExt = normalized.replace(/\.uasset$/i, "");
  const stem = withoutExt.split("/").pop();
  if (!stem) {
    return "";
  }

  return `/Game/ConZ_Files/${withoutExt}.${stem}_C`;
}

function sectionPriority(name) {
  if (String(name || "").startsWith("Ингредиент ")) {
    return 16;
  }

  const order = {
    "Стартовый предмет": 1,
    "Условия выдачи": 2,
    "Типы персонажей": 3,
    "Результат": 4,
    "Время крафта": 5,
    "Награда": 6,
    "Справка": 7,
    "Основное": 8,
    "Тип вещества": 9,
    "Влияние на характеристики": 10,
    "Всасывание": 11,
    "Выведение": 12,
    "Поведение NPC": 13,
    "Состав события": 14,
    "Точки появления": 15,
    "Тайминги": 16,
    "Анимации": 17,
    "Симптомы": 18,
    "Урон": 19,
    "Опьянение": 20,
    "Движение": 21,
    "Персонаж": 22,
    "Защита": 23,
    "Пороги эффекта": 24,
    "Уровни торговли": 25,
    "Радиация": 26,
    "Экономика": 27,
    "Крафт": 28,
    "Общие": 99
  };
  return order[name] ?? 50;
}

function schemaActionableTargets(schema) {
  const listTargets = Array.isArray(schema?.listTargets) ? schema.listTargets : [];
  return listTargets.filter((target) => target.supportsAddReference && target.referencePickerKind);
}

function cssEscapeValue(value) {
  if (typeof CSS !== "undefined" && typeof CSS.escape === "function") {
    return CSS.escape(String(value));
  }

  return String(value)
    .replace(/\\/g, "\\\\")
    .replace(/"/g, '\\"');
}

function findFieldInputElement(fieldPath) {
  if (!fieldPath) {
    return null;
  }

  return document.querySelector(`[data-field-path="${cssEscapeValue(fieldPath)}"]`);
}

function syncFieldInputElement(fieldPath, value) {
  const input = findFieldInputElement(fieldPath);
  if (!input || document.activeElement === input) {
    return;
  }

  if (input instanceof HTMLInputElement && input.type === "checkbox") {
    input.checked = String(value).toLowerCase() === "true";
    return;
  }

  if ("value" in input && String(input.value) !== String(value)) {
    input.value = String(value);
  }
}

function sceneUsesFieldPath(fieldPath) {
  const scene = state.modding.currentScene;
  return Boolean(fieldPath && scene?.fieldPaths instanceof Set && scene.fieldPaths.has(fieldPath));
}

function setCurrentFieldValue(fieldPath, value, options = {}) {
  const nextValue = String(value ?? "");
  state.modding.currentFieldValues.set(fieldPath, nextValue);

  if (Object.prototype.hasOwnProperty.call(options, "displayValue")) {
    state.modding.currentFieldDisplayValues.set(fieldPath, options.displayValue);
  }

  if (options.syncDom !== false) {
    syncFieldInputElement(fieldPath, nextValue);
  }

  if (options.renderScene !== false && sceneUsesFieldPath(fieldPath)) {
    renderSceneEditor();
  }
}

function attachFieldInputMeta(input, field) {
  if (input && field?.fieldPath && typeof input.setAttribute === "function") {
    input.setAttribute("data-field-path", field.fieldPath);
  }

  return input;
}

function isMapSchema(schema) {
  const relativePath = String(schema?.relativePath || state.modding.selectedAsset?.relativePath || "").trim().toLowerCase();
  return relativePath.endsWith(".umap");
}

function parseSceneFieldComponent(field) {
  const path = String(field?.fieldPath || "").trim();
  let match = path.match(/^(.*)\/vc:(x|y|z)$/i);
  if (match) {
    return {
      kind: "vector",
      basePath: match[1],
      component: match[2].toLowerCase()
    };
  }

  match = path.match(/^(.*)\/rc:(pitch|yaw|roll)$/i);
  if (match) {
    return {
      kind: "rotator",
      basePath: match[1],
      component: match[2].toLowerCase()
    };
  }

  return null;
}

function stripSceneComponentLabel(label) {
  return String(label || "")
    .trim()
    .replace(/\s*(?:\.|\/)\s*(x|y|z|pitch|yaw|roll)$/i, "")
    .replace(/\s+/g, " ")
    .trim();
}

function getSceneGroupParentPath(basePath) {
  const idx = String(basePath || "").lastIndexOf("/");
  return idx > 0 ? basePath.slice(0, idx) : String(basePath || "");
}

function isSupportedScenePositionLabel(label) {
  const text = String(label || "").toLowerCase();
  if (!text || text.includes("масштаб")) {
    return false;
  }

  return text.includes("точка на карте")
    || text.includes("координаты")
    || text.includes("точка маршрута")
    || text.includes("точка появления")
    || text.includes("где появляется")
    || text.includes("где появляются")
    || text.includes("метки торговца")
    || text.includes("спавна транспорта")
    || text.includes("зона взаимодействия")
    || text.includes("корневая точка");
}

function isSupportedSceneRotationLabel(label) {
  const text = String(label || "").toLowerCase();
  return text.includes("поворот точки") || text.includes("rotation");
}

function classifySceneNodeKind(label) {
  const text = String(label || "").toLowerCase();
  if (text.includes("маршрут робота") || text.includes("точка маршрута")) {
    return "route";
  }

  if (text.includes("спавна транспорта")) {
    return "vehicle";
  }

  if (text.includes("точка появления npc")) {
    return "npc";
  }

  if (text.includes("запаса торговца")) {
    return "trader-depot";
  }

  if (text.includes("купленных товаров")) {
    return "trader-pickup";
  }

  if (text.includes("метки торговца") || text.includes("торговца")) {
    return "trader";
  }

  if (text.includes("взаимодействия")) {
    return "interaction";
  }

  if (text.includes("квест")) {
    return "quest";
  }

  if (text.includes("робот")) {
    return "sentry";
  }

  return "point";
}

function sceneKindTitle(kind) {
  return {
    route: "Маршрут робота",
    vehicle: "Точка транспорта",
    npc: "Точка NPC",
    "trader-depot": "Запас торговца",
    "trader-pickup": "Выдача покупок",
    trader: "Точка торговца",
    interaction: "Зона NPC",
    quest: "Квестовая точка",
    sentry: "Точка робота",
    point: "Точка на карте"
  }[kind] || "Точка на карте";
}

function sceneKindColor(kind) {
  return {
    route: "#ffd166",
    vehicle: "#ff8b6b",
    npc: "#7ee787",
    "trader-depot": "#eebd5c",
    "trader-pickup": "#9fd8ff",
    trader: "#66c5ff",
    interaction: "#c792ea",
    quest: "#89ddff",
    sentry: "#ff6b6b",
    point: "#d8e6f7"
  }[kind] || "#d8e6f7";
}

function sceneKindBadge(kind) {
  return {
    route: "М",
    vehicle: "ТС",
    npc: "NPC",
    "trader-depot": "СК",
    "trader-pickup": "ВЫ",
    trader: "ТР",
    interaction: "ЗН",
    quest: "КВ",
    sentry: "РБ",
    point: "•"
  }[kind] || "•";
}

function simplifySceneOwnerName(owner, kind) {
  const text = String(owner || "").trim();
  if (!text) {
    return sceneKindTitle(kind);
  }

  if (/зона взаимодействия npc/i.test(text)) {
    const number = text.match(/#\s*(\d+)/i)?.[1];
    return number ? `Точка NPC #${number}` : "Точка NPC";
  }

  if (/спавнер транспорта/i.test(text)) {
    const number = text.match(/#\s*(\d+)/i)?.[1];
    return number ? `Транспорт #${number}` : "Транспорт";
  }

  if (/торговец/i.test(text)) {
    return text
      .replace(/торговец-рыбак/ig, "Торговец-рыбак")
      .replace(/sedentary/ig, "")
      .trim();
  }

  if (/робот/i.test(text)) {
    return text;
  }

  return text;
}

function buildSceneFriendlyName(node) {
  const parts = String(node?.label || "")
    .split("/")
    .map((part) => part.trim())
    .filter(Boolean);
  const owner = simplifySceneOwnerName(parts[0] || "", node.kind);

  switch (node.kind) {
    case "trader-pickup":
      return `${owner} / выдача покупок`;
    case "trader-depot":
      return `${owner} / место запаса`;
    case "npc":
      return `${owner} / место появления`;
    case "vehicle":
      return `${owner} / место транспорта`;
    case "route":
      return node.routeOrder
        ? `${owner} / точка маршрута ${node.routeOrder}`
        : `${owner} / точка маршрута`;
    case "interaction":
      return `${owner} / зона общения`;
    default:
      return parts.slice(0, 2).join(" / ") || owner;
  }
}

function describeSceneNodeUsage(node) {
  switch (node.kind) {
    case "trader-pickup":
      return "Куда у этого торговца будут падать купленные вещи.";
    case "trader-depot":
      return "Где стоит запас или склад этой торговой точки.";
    case "npc":
      return "Где именно появляется NPC или продавец на этой точке.";
    case "vehicle":
      return "Где стоит техника или транспортная точка на карте.";
    case "route":
      return "Одна из точек обхода робота. Порядок маршрута можно видеть по номеру на карте.";
    case "interaction":
      return "Точка и зона, через которую игрок подходит к NPC и открывает взаимодействие.";
    default:
      return "Игровая точка на карте, которую можно безопасно передвинуть в пределах поддерживаемой сцены.";
  }
}

function extractSceneRouteGroupKey(label) {
  const raw = String(label || "").trim();
  const direct = raw.match(/^(.*?маршрут робота\s*\d+)/i);
  if (direct) {
    return direct[1].trim();
  }

  const fallback = raw.match(/^(.*?)(?:\s*\/\s*точка маршрута.*)$/i);
  return fallback ? fallback[1].trim() : "";
}

function computeSceneBounds(nodes) {
  if (!nodes.length) {
    return {
      minX: -500,
      maxX: 500,
      minY: -500,
      maxY: 500
    };
  }

  let minX = nodes[0].x;
  let maxX = nodes[0].x;
  let minY = nodes[0].y;
  let maxY = nodes[0].y;
  for (const node of nodes) {
    minX = Math.min(minX, node.x);
    maxX = Math.max(maxX, node.x);
    minY = Math.min(minY, node.y);
    maxY = Math.max(maxY, node.y);
  }

  const spanX = Math.max(300, maxX - minX);
  const spanY = Math.max(300, maxY - minY);
  const pad = Math.max(140, Math.max(spanX, spanY) * 0.18);
  return {
    minX: minX - pad,
    maxX: maxX + pad,
    minY: minY - pad,
    maxY: maxY + pad
  };
}

function computeSceneProjection(bounds) {
  const worldWidth = Math.max(1, bounds.maxX - bounds.minX);
  const worldHeight = Math.max(1, bounds.maxY - bounds.minY);
  const scale = Math.min(
    (SCENE_VIEWBOX_WIDTH - SCENE_VIEWBOX_PADDING * 2) / worldWidth,
    (SCENE_VIEWBOX_HEIGHT - SCENE_VIEWBOX_PADDING * 2) / worldHeight
  );
  const contentWidth = worldWidth * scale;
  const contentHeight = worldHeight * scale;
  return {
    scale,
    offsetX: (SCENE_VIEWBOX_WIDTH - contentWidth) / 2,
    offsetY: (SCENE_VIEWBOX_HEIGHT - contentHeight) / 2
  };
}

function projectScenePoint(scene, x, y) {
  const bounds = scene.bounds;
  const projection = scene.projection;
  return {
    x: projection.offsetX + (x - bounds.minX) * projection.scale,
    y: SCENE_VIEWBOX_HEIGHT - (projection.offsetY + (y - bounds.minY) * projection.scale)
  };
}

function sceneNodeMatchesKindFilter(node, filterKind) {
  if (!filterKind || filterKind === "all") {
    return true;
  }

  if (filterKind === "trader") {
    return node.kind === "trader" || node.kind === "trader-depot" || node.kind === "trader-pickup";
  }

  return node.kind === filterKind;
}

function sceneNodeMatchesSearch(node, searchTerm) {
  const normalized = String(searchTerm || "").trim().toLowerCase();
  if (!normalized) {
    return true;
  }

  const haystack = `${buildSceneFriendlyName(node)} ${node.label} ${sceneKindTitle(node.kind)}`.toLowerCase();
  return normalized.split(/\s+/).every((chunk) => haystack.includes(chunk));
}

function getVisibleSceneNodes(scene) {
  const filterKind = state.modding.currentSceneFilterKind;
  const searchTerm = state.modding.currentSceneSearch;
  return scene.nodes.filter((node) => sceneNodeMatchesKindFilter(node, filterKind) && sceneNodeMatchesSearch(node, searchTerm));
}

function computeSceneFocusBounds(visibleNodes, selectedNode) {
  if (!visibleNodes.length) {
    return computeSceneBounds([]);
  }

  if (state.modding.currentSceneFocusMode === "selected" && selectedNode) {
    const radius = 260;
    return {
      minX: selectedNode.x - radius,
      maxX: selectedNode.x + radius,
      minY: selectedNode.y - radius,
      maxY: selectedNode.y + radius
    };
  }

  return computeSceneBounds(visibleNodes);
}

function scenePointerToWorld(svg, clientX, clientY, scene) {
  const rect = svg.getBoundingClientRect();
  if (!rect.width || !rect.height) {
    return { x: 0, y: 0 };
  }

  const vx = ((clientX - rect.left) / rect.width) * SCENE_VIEWBOX_WIDTH;
  const vy = ((clientY - rect.top) / rect.height) * SCENE_VIEWBOX_HEIGHT;
  const worldX = scene.bounds.minX + ((vx - scene.projection.offsetX) / scene.projection.scale);
  const worldY = scene.bounds.minY + (((SCENE_VIEWBOX_HEIGHT - vy) - scene.projection.offsetY) / scene.projection.scale);
  return { x: worldX, y: worldY };
}

function svgElement(name, attrs = {}) {
  const node = document.createElementNS("http://www.w3.org/2000/svg", name);
  for (const [key, value] of Object.entries(attrs)) {
    node.setAttribute(key, String(value));
  }
  return node;
}

function buildSceneModel(schema) {
  if (!isMapSchema(schema)) {
    return null;
  }

  const vectorGroups = new Map();
  const rotationGroups = new Map();
  const fields = Array.isArray(schema?.fields) ? schema.fields : [];
  const fieldPaths = new Set();

  fields.forEach((field, index) => {
    const componentInfo = parseSceneFieldComponent(field);
    if (!componentInfo) {
      return;
    }

    const groupMap = componentInfo.kind === "vector" ? vectorGroups : rotationGroups;
    if (!groupMap.has(componentInfo.basePath)) {
      groupMap.set(componentInfo.basePath, {
        basePath: componentInfo.basePath,
        parentPath: getSceneGroupParentPath(componentInfo.basePath),
        label: stripSceneComponentLabel(field.label),
        order: index,
        componentFieldPaths: {},
        componentEditable: {},
        componentValues: {}
      });
    }

    const group = groupMap.get(componentInfo.basePath);
    group.label = stripSceneComponentLabel(field.label) || group.label;
    group.componentFieldPaths[componentInfo.component] = field.fieldPath;
    group.componentEditable[componentInfo.component] = field.editable !== false;
    group.componentValues[componentInfo.component] = toNumberSafe(
      state.modding.currentFieldValues.get(field.fieldPath) ?? field.currentValue,
      0
    );
    fieldPaths.add(field.fieldPath);
  });

  const rotationsByParent = new Map();
  for (const rotation of rotationGroups.values()) {
    if (!isSupportedSceneRotationLabel(rotation.label)) {
      continue;
    }

    if (!rotationsByParent.has(rotation.parentPath)) {
      rotationsByParent.set(rotation.parentPath, []);
    }

    rotationsByParent.get(rotation.parentPath).push(rotation);
  }

  const nodes = [];
  for (const group of vectorGroups.values()) {
    if (!isSupportedScenePositionLabel(group.label)) {
      continue;
    }

    if (!Object.prototype.hasOwnProperty.call(group.componentValues, "x")
      || !Object.prototype.hasOwnProperty.call(group.componentValues, "y")) {
      continue;
    }

    const rotationCandidates = rotationsByParent.get(group.parentPath) || [];
    const rotation = rotationCandidates
      .slice()
      .sort((a, b) => Math.abs(a.order - group.order) - Math.abs(b.order - group.order))[0] || null;

    const label = group.label;
    const kind = classifySceneNodeKind(label);
    nodes.push({
      id: group.basePath,
      label,
      kind,
      color: sceneKindColor(kind),
      badge: sceneKindBadge(kind),
      order: group.order,
      routeGroupKey: kind === "route" ? extractSceneRouteGroupKey(label) : "",
      x: group.componentValues.x,
      y: group.componentValues.y,
      z: group.componentValues.z ?? 0,
      pitch: rotation?.componentValues?.pitch ?? 0,
      yaw: rotation?.componentValues?.yaw ?? 0,
      roll: rotation?.componentValues?.roll ?? 0,
      editable: group.componentEditable.x !== false && group.componentEditable.y !== false,
      fieldPaths: {
        x: group.componentFieldPaths.x || "",
        y: group.componentFieldPaths.y || "",
        z: group.componentFieldPaths.z || ""
      },
      rotationFieldPaths: {
        pitch: rotation?.componentFieldPaths?.pitch || "",
        yaw: rotation?.componentFieldPaths?.yaw || "",
        roll: rotation?.componentFieldPaths?.roll || ""
      }
    });
  }

  nodes.sort((a, b) => a.order - b.order);

  const links = [];
  const routeGroups = new Map();
  nodes.forEach((node) => {
    if (!node.routeGroupKey) {
      return;
    }

    if (!routeGroups.has(node.routeGroupKey)) {
      routeGroups.set(node.routeGroupKey, []);
    }

    routeGroups.get(node.routeGroupKey).push(node);
  });

  for (const routeNodes of routeGroups.values()) {
    routeNodes.sort((a, b) => a.order - b.order);
    routeNodes.forEach((node, index) => {
      node.badge = String(index + 1);
      node.routeOrder = index + 1;
      if (index > 0) {
        links.push({
          fromId: routeNodes[index - 1].id,
          toId: node.id
        });
      }
    });
  }

  const view = state.modding.currentSceneDrag?.view || null;
  const bounds = view?.bounds || computeSceneBounds(nodes);
  return {
    nodes,
    links,
    fieldPaths,
    bounds,
    projection: view?.projection || computeSceneProjection(bounds)
  };
}

function getReferencePickerBaseName(pickerKind) {
  const normalized = String(pickerKind || "").trim().toLowerCase();
  return normalized === "bodyeffect-side-effect"
    ? "последствие"
    : normalized === "quest-giver"
      ? "источник квестов"
    : normalized === "skill-blueprint-asset" || normalized === "skill-blueprint-reference"
        || normalized === "skill-asset" || normalized === "skill-reference"
      ? "навык"
    : normalized === "quest-asset" || normalized === "quest-reference"
      ? "квест"
    : normalized === "item-asset" || normalized === "item-reference"
      ? "предмет"
    : normalized === "item-spawner-preset" || normalized === "regular-item-spawner-preset"
      ? "пресет дропа"
    : normalized === "advanced-item-spawner-preset"
      || normalized === "container-loot-preset"
      || normalized === "examine-data-preset"
      ? "контейнерный набор"
    : normalized === "advanced-item-spawner-subpreset"
      || normalized === "container-subpreset-preset"
      ? "подпакет лута"
    : normalized === "gameevent-primary-loadout"
      || normalized === "gameevent-secondary-loadout"
      || normalized === "gameevent-tertiary-loadout"
      || normalized === "gameevent-outfit-loadout"
      || normalized === "gameevent-mandatory-loadout"
      || normalized === "gameevent-support-loadout"
      ? "набор"
    : normalized === "cargo-drop-encounter-class"
      ? "защиту"
    : normalized === "plant-species-asset" || normalized === "plant-species"
      ? "растение"
    : normalized === "plant-pest-asset" || normalized === "plant-pest"
      ? "вредителя"
    : normalized === "plant-disease-asset" || normalized === "plant-disease"
      ? "болезнь"
    : normalized === "fish-species-asset" || normalized === "fish-species"
      ? "вид рыбы"
      : "элемент";
}

function buildReferenceActionLabel(pickerKind, mode = "add") {
  const base = getReferencePickerBaseName(pickerKind);

  return mode === "choose"
    ? `Выбрать ${base}`
    : `Добавить ${base}`;
}

function buildReferenceSearchPlaceholder(pickerKind, fallbackText) {
  const base = getReferencePickerBaseName(pickerKind);
  if (!base || base === "элемент") {
    return fallbackText || "Введи хотя бы 2 буквы для поиска";
  }

  return `Введи хотя бы 2 буквы, чтобы найти ${base}`;
}

function buildPickerIntroText(pickerKind, hasQuickHints, isEmptyTerm) {
  if (isEmptyTerm) {
    return hasQuickHints
      ? "Можно нажать готовую подсказку выше или сразу выбрать вариант из списка ниже."
      : "Нажми в поле, чтобы увидеть доступные варианты, или начни поиск по названию.";
  }

  return hasQuickHints
    ? "Ничего не найдено. Попробуй более короткое слово или нажми одну из подсказок выше."
    : "Ничего не найдено. Попробуй более короткое или более общее слово.";
}

function describeSchemaMeta(schema) {
  const fields = Array.isArray(schema?.fields) ? schema.fields : [];
  const editableCount = fields.filter((field) => field.editable !== false).length;
  const readonlyCount = fields.filter((field) => field.editable === false).length;
  const listTargets = Array.isArray(schema?.listTargets) ? schema.listTargets : [];
  const actionableTargets = schemaActionableTargets(schema);
  const parts = [];

  if (schema?.sourceKind === "preview") {
    parts.push("Открыт уже изменённый вариант");
  }

  if (editableCount > 0) {
    parts.push(`Есть прямые настройки: ${editableCount}`);
  } else if (actionableTargets.length > 0) {
    parts.push("Сначала нужно добавить или изменить состав системы");
  } else if (readonlyCount > 0) {
    parts.push("Здесь в основном связи и справка из игры");
  } else {
    parts.push("Понятных прямых настроек пока не найдено");
  }

  if (listTargets.length > 0) {
    parts.push(`Блоков состава: ${listTargets.length}`);
  }

  return parts.join(". ") + ".";
}

function describeStagedItem(item) {
  const fieldCount = Array.isArray(item?.edits) ? item.edits.length : 0;
  const listCount = Array.isArray(item?.listEdits) ? item.listEdits.length : 0;
  if (fieldCount > 0 && listCount > 0) {
    return `Есть прямые настройки (${fieldCount}) и изменения состава (${listCount}).`;
  }
  if (fieldCount > 0) {
    return `Есть прямые настройки: ${fieldCount}.`;
  }
  if (listCount > 0) {
    return `Есть изменения состава: ${listCount}.`;
  }
  return "Изменений пока нет.";
}

function getQuickPickerHints(pickerKind) {
  const normalized = String(pickerKind || "").trim().toLowerCase();
  if (normalized === "bodyeffect-side-effect") {
    return [
      { label: "Бонус или штраф к силе", term: "сила" },
      { label: "Бонус или штраф к выносливости", term: "выносливость" },
      { label: "Бонус или штраф к скорости", term: "скорость" },
      { label: "Постепенный урон", term: "урон" },
      { label: "Штраф к интеллекту", term: "интеллект" },
      { label: "Штраф к ловкости", term: "ловкость" }
    ];
  }

  if (normalized === "foreign-substance-attribute") {
    return [
      { label: "Агент инфекции", term: "инфекция" },
      { label: "Кофеин", term: "кофеин" },
      { label: "Антибиотики", term: "антибиотик" },
      { label: "Активированный уголь", term: "уголь" }
    ];
  }

  if (normalized === "quest-giver") {
    return [
      { label: "Телефон", term: "телефон" },
      { label: "Доска заданий", term: "доска" },
      { label: "Механик", term: "механик" },
      { label: "Оружейник", term: "оружейник" }
    ];
  }

  if (normalized === "quest-asset" || normalized === "quest-reference") {
    return [
      { label: "Стартовые квесты", term: "телефон" },
      { label: "Квесты механика", term: "механик" },
      { label: "Квесты оружейника", term: "оружейник" },
      { label: "Квесты общих товаров", term: "общие товары" }
    ];
  }

  if (
    normalized === "skill-asset" ||
    normalized === "skill-reference" ||
    normalized === "skill-blueprint-asset" ||
    normalized === "skill-blueprint-reference"
  ) {
    return [
      { label: "Инженерия", term: "инженерия" },
      { label: "Медицина", term: "медицина" },
      { label: "Вождение", term: "вождение" },
      { label: "Внимательность", term: "внимательность" }
    ];
  }

  if (normalized === "item-asset" || normalized === "item-reference") {
    return [
      { label: "Домкрат", term: "домкрат" },
      { label: "Металлолом", term: "металлолом" },
      { label: "Ремкомплект машины", term: "ремкомплект" },
      { label: "Бинт", term: "бинт" }
    ];
  }

  if (normalized === "item-spawner-preset" || normalized === "regular-item-spawner-preset") {
    return [
      { label: "Военный грузовой дроп", term: "cargo" },
      { label: "Катана", term: "katana" },
      { label: "Медицинский дроп", term: "medical" },
      { label: "Машинный дроп", term: "car" }
    ];
  }

  if (
    normalized === "advanced-item-spawner-preset" ||
    normalized === "advanced-item-spawner-subpreset" ||
    normalized === "examine-data-preset" ||
    normalized === "container-loot-preset" ||
    normalized === "container-subpreset-preset"
  ) {
    return [
      { label: "Контейнер: машина", term: "car" },
      { label: "Контейнер: katana", term: "katana" },
      { label: "Контейнер: медицина", term: "medical" },
      { label: "Ключ-карта", term: "key card" }
    ];
  }

  if (normalized === "gameevent-primary-loadout") {
    return [
      { label: "Автоматы и винтовки", term: "rifles" },
      { label: "AK и AKM", term: "ak" },
      { label: "M16 и M82", term: "m16" },
      { label: "MP5 и UMP45", term: "mp5" }
    ];
  }

  if (normalized === "gameevent-secondary-loadout") {
    return [
      { label: "Пистолеты", term: "pistols" },
      { label: "M9", term: "m9" },
      { label: "Block 21", term: "block" },
      { label: "Desert Eagle", term: "deagle" }
    ];
  }

  if (normalized === "gameevent-tertiary-loadout") {
    return [
      { label: "Ближний бой", term: "melee" },
      { label: "Катана", term: "katana" },
      { label: "Топоры", term: "axe" },
      { label: "Bushman", term: "bushman" }
    ];
  }

  if (normalized === "gameevent-outfit-loadout") {
    return [
      { label: "Военная форма", term: "military" },
      { label: "MMA", term: "mma" },
      { label: "Bear Outfit", term: "bear" },
      { label: "Одежда", term: "outfit" }
    ];
  }

  if (normalized === "gameevent-mandatory-loadout" || normalized === "gameevent-support-loadout") {
    return [
      { label: "Гранаты", term: "grenades" },
      { label: "Обязательное снаряжение", term: "mandatory" },
      { label: "Расходники события", term: "gear" }
    ];
  }

  if (normalized === "cargo-drop-encounter-class") {
    return [
      { label: "Обычная охрана дропа", term: "cargo drop event" },
      { label: "Летающий страж", term: "flying guardian" },
      { label: "Грузовой дроп", term: "cargo" }
    ];
  }

  if (normalized === "item-spawner-preset" || normalized === "regular-item-spawner-preset") {
    return [
      { label: "Военный лут", term: "military" },
      { label: "Дом", term: "house" },
      { label: "Полиция", term: "police" },
      { label: "Медицинский", term: "medical" }
    ];
  }

  if (
    normalized === "advanced-item-spawner-preset" ||
    normalized === "advanced-item-spawner-subpreset" ||
    normalized === "container-loot-preset" ||
    normalized === "container-subpreset-preset" ||
    normalized === "examine-data-preset"
  ) {
    return [
      { label: "Buildings", term: "buildings" },
      { label: "Bunker", term: "bunker" },
      { label: "Locker", term: "locker" },
      { label: "Bathroom", term: "bathroom" },
      { label: "Military", term: "military" },
      { label: "Medical", term: "medical" }
    ];
  }

  if (normalized === "skill-asset" || normalized === "skill-reference"
    || normalized === "skill-blueprint-asset" || normalized === "skill-blueprint-reference") {
    return [
      { label: "Вождение", term: "driving" },
      { label: "Медицина", term: "medical" },
      { label: "Взлом", term: "lockpicking" },
      { label: "Маскировка", term: "camouflage" }
    ];
  }

  if (normalized === "fish-species-asset" || normalized === "fish-species") {
    return [
      { label: "Карп и карась", term: "карп" },
      { label: "Сом", term: "сом" },
      { label: "Окунь", term: "окунь" },
      { label: "Уклейка", term: "уклейка" }
    ];
  }

  if (normalized === "plant-species-asset" || normalized === "plant-species") {
    return [
      { label: "Брокколи", term: "брокколи" },
      { label: "Кукуруза", term: "кукуруза" },
      { label: "Томат", term: "томат" },
      { label: "Картофель", term: "картофель" }
    ];
  }

  if (normalized === "plant-pest-asset" || normalized === "plant-pest") {
    return [
      { label: "Тля", term: "тля" },
      { label: "Слизни", term: "слизни" },
      { label: "Кузнечик", term: "кузнечик" },
      { label: "Черви", term: "черви" }
    ];
  }

  if (normalized === "plant-disease-asset" || normalized === "plant-disease") {
    return [
      { label: "Гниль", term: "гниль" },
      { label: "Ржавчина", term: "ржавчина" },
      { label: "Плесень", term: "плесень" },
      { label: "Ложная мучнистая роса", term: "роса" }
    ];
  }

  if (normalized === "encounter-character-preset") {
    return [
      { label: "Военные зомби", term: "зомби" },
      { label: "Вооружённые NPC", term: "npc" },
      { label: "Животные", term: "животные" }
    ];
  }

  if (normalized === "encounter-npc-class") {
    return [
      { label: "Охранник", term: "охранник" },
      { label: "Скиталец", term: "скиталец" },
      { label: "Радиация", term: "радиация" }
    ];
  }

  if (normalized === "crafting-ingredient-asset" || normalized === "crafting-ingredient") {
    return [
      { label: "Верёвка и нити", term: "верёвка" },
      { label: "Палки и доски", term: "палка" },
      { label: "Тряпки и ткань", term: "тряпка" },
      { label: "Инструменты", term: "инструмент" },
      { label: "Металл и проволока", term: "проволока" }
    ];
  }

  return [];
}

function getFieldQuickPickerHints(field) {
  const label = String(field?.label || "").toLowerCase();
  const prompt = String(field?.referencePickerPrompt || "").toLowerCase();
  if (label.includes("семян") || prompt.includes("семян")) {
    return [
      { label: "Семена яблока", term: "apple seeds" },
      { label: "Семена брокколи", term: "broccoli seeds" },
      { label: "Семена кукурузы", term: "corn seeds" },
      { label: "Семена тыквы", term: "pumpkin seeds" }
    ];
  }

  return getQuickPickerHints(field?.referencePickerKind);
}

function queueReferenceOption(target, option) {
  if (!target || !option) {
    return;
  }

  queueListEdit({
    targetPath: target.targetPath,
    targetLabel: target.label,
    action: "add-reference",
    index: null,
    sourceIndex: Math.max(0, Number(target.itemCount || 0) - 1),
    templateJson: null,
    rawValue: option.value,
    rawLabel: option.label
  });
}

async function queueQuickReferenceSearch(target, term) {
  const options = await fetchReferenceOptions(target.referencePickerKind, term, 8);
  const rows = Array.isArray(options) ? options : [];
  if (!rows.length) {
    throw new Error("Для этого быстрого действия пока ничего не найдено. Попробуй поиск ниже.");
  }

  queueReferenceOption(target, rows[0]);
}

function buildGuidedEmptyState(schema) {
  const actionableTargets = schemaActionableTargets(schema);
  if (!actionableTargets.length) {
    return null;
  }

  const primaryTarget = actionableTargets[0];
  const wrap = document.createElement("div");
  wrap.className = "guided-empty-state";

  const title = document.createElement("div");
  title.className = "guided-empty-title";
  title.textContent = "Эта система пока пустая";

  const text = document.createElement("div");
  text.className = "guided-empty-text";
  text.textContent = primaryTarget.referencePickerKind === "bodyeffect-side-effect"
    ? "Она сама почти ничего не делает, пока ты не добавишь в неё последствия. Сначала выбери, что именно должно происходить с персонажем."
    : "В этой системе главное не числа, а состав. Сначала добавь нужные игровые элементы, а потом открой их новые настройки.";

  const tips = document.createElement("div");
  tips.className = "guided-empty-steps";
  [
    "1. Выбери готовое действие ниже.",
    "2. Оно попадёт в очередь изменений состава.",
    "3. Затем нажми кнопку «Показать результат и открыть новые настройки»."
  ].forEach((line) => {
    const item = document.createElement("div");
    item.className = "guided-empty-step";
    item.textContent = line;
    tips.appendChild(item);
  });

  wrap.append(title, text, tips);

  const hints = getQuickPickerHints(primaryTarget.referencePickerKind);
  if (hints.length) {
    const actionsTitle = document.createElement("div");
    actionsTitle.className = "guided-empty-subtitle";
    actionsTitle.textContent = "Быстрые действия";
    wrap.appendChild(actionsTitle);

    const actionRow = document.createElement("div");
    actionRow.className = "guided-empty-actions";
    hints.forEach((hint) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "quick-action-chip";
      btn.textContent = hint.label;
      btn.addEventListener("click", async () => {
        try {
          btn.disabled = true;
          await queueQuickReferenceSearch(primaryTarget, hint.term);
          renderCurrentListOps();
        } catch (error) {
          showError(error);
        } finally {
          btn.disabled = false;
        }
      });
      actionRow.appendChild(btn);
    });
    wrap.appendChild(actionRow);
  }

  const jumpRow = document.createElement("div");
  jumpRow.className = "guided-empty-footer";

  const jumpBtn = document.createElement("button");
  jumpBtn.type = "button";
  jumpBtn.textContent = "Открыть блок состава ниже";
  jumpBtn.addEventListener("click", () => {
    document.getElementById("listTargetRows")?.scrollIntoView({ behavior: "smooth", block: "start" });
  });
  jumpRow.appendChild(jumpBtn);

  wrap.appendChild(jumpRow);
  return wrap;
}

function modPageCount() {
  return Math.max(1, Math.ceil(state.modding.total / state.modding.pageSize));
}

function getVisibleAssets() {
  const onlyEditable = el("modOnlyEditableCheck")?.checked === true;
  state.modding.showOnlyEditable = onlyEditable;
  if (!onlyEditable) {
    return [...state.modding.assets];
  }

  return state.modding.assets.filter((asset) => asset.supportsSafeEdits);
}

function syncSelectedAssetWithVisibleList() {
  const visibleAssets = getVisibleAssets();
  if (!visibleAssets.length) {
    state.modding.selectedAssetId = "";
    state.modding.selectedAsset = null;
    return visibleAssets;
  }

  state.modding.selectedAsset =
    visibleAssets.find((asset) => asset.assetId === state.modding.selectedAssetId) || null;
  if (!state.modding.selectedAsset) {
    state.modding.selectedAssetId = "";
  }
  return visibleAssets;
}

async function api(url, options) {
  const response = await fetch(url, options);
  const text = await response.text();
  let payload = null;

  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = text;
    }
  }

  if (!response.ok) {
    const message =
      payload && typeof payload === "object" && typeof payload.error === "string"
        ? payload.error
        : typeof payload === "string"
          ? payload
          : `HTTP ${response.status}`;
    throw new Error(message);
  }

  return payload;
}

async function fetchReferenceOptions(pickerKind, term, limit = 12) {
  const query = new URLSearchParams({
    pickerKind: String(pickerKind || ""),
    term: String(term || ""),
    limit: String(limit)
  });
  return api(`/api/modding/reference-options?${query.toString()}`);
}

async function fetchCustomVisualModels() {
  const models = await api("/api/custom-visual-assets?kind=model");
  state.modding.customVisualModels = Array.isArray(models) ? models : [];
  renderStudioFlowBar();
  return state.modding.customVisualModels;
}

function formatCustomVisualKind(kind) {
  return {
    "static-mesh": "модель",
    "skeletal-mesh": "скелетная модель",
    material: "материал",
    texture: "текстура"
  }[String(kind || "").toLowerCase()] || "ассет";
}

function formatModelBounds(bounds) {
  if (!bounds) {
    return "";
  }

  const sx = Number(bounds.sizeX);
  const sy = Number(bounds.sizeY);
  const sz = Number(bounds.sizeZ);
  if (![sx, sy, sz].every(Number.isFinite)) {
    return "";
  }

  const fmt = (value) => value.toFixed(3).replace(/\.?0+$/, "");
  return `${fmt(sx)} x ${fmt(sy)} x ${fmt(sz)}`;
}

function formatCompactInteger(value) {
  const number = Number(value || 0);
  return Number.isFinite(number) ? number.toLocaleString("ru-RU") : "0";
}

function formatRawModelPartRole(role) {
  return {
    "armor-helmet": "броня: шлем",
    "armor-vest": "броня: торс/жилет",
    "armor-arms": "броня: руки",
    "armor-legs": "броня: ноги",
    "armor-belt": "броня: пояс",
    "armor-hands": "броня: кисти/перчатки",
    "armor-boots": "броня: ботинки",
    "armor-detail": "броня: деталь",
    "query-proxy": "query/collision",
    weapon: "оружейный модуль",
    engine: "двигатель/ротор",
    "seat-interior": "салон/посадка",
    wing: "крыло/аэроповерхность",
    "tail-control": "хвост/руль",
    "landing-gear": "шасси/опора",
    hull: "корпус/chassis",
    detail: "деталь"
  }[String(role || "").toLowerCase()] || String(role || "деталь");
}

function buildArmorSetPlanGroups(parts) {
  const armorParts = (Array.isArray(parts) ? parts : [])
    .filter((part) => String(part?.role || "").toLowerCase().startsWith("armor-"));
  if (!armorParts.length) {
    return [];
  }

  const definitions = [
    {
      key: "helmet",
      title: "Шлем",
      roles: ["armor-helmet"],
      target: "Слот шлема / UpperHeadSocket",
      note: "Cook отдельно, запас вокруг головы 3-6%, не менять внутренний component export."
    },
    {
      key: "torso",
      title: "Торс и жилет",
      roles: ["armor-vest", "armor-belt"],
      target: "Armor vest или torso protection",
      note: "Жилет и пояс лучше вести одним worn visual, чтобы не было щели на талии."
    },
    {
      key: "arms",
      title: "Руки и плечи",
      roles: ["armor-arms", "armor-hands"],
      target: "Куртка/рукава или перчатки",
      note: "Для анимаций нужны skin weights; статичные детали можно объединять с торсом."
    },
    {
      key: "legs",
      title: "Ноги",
      roles: ["armor-legs", "armor-boots"],
      target: "Pants / boots",
      note: "Нижнюю броню делить на pants и boots только если части не пересекают колени."
    },
    {
      key: "details",
      title: "Остальные детали",
      roles: ["armor-detail"],
      target: "Ближайший worn slot",
      note: "Мелкие детали объединяются с ближайшим элементом комплекта."
    }
  ];

  return definitions
    .map((definition) => {
      const selected = armorParts.filter((part) => definition.roles.includes(String(part.role || "").toLowerCase()));
      const triangles = selected.reduce((sum, part) => sum + Number(part.triangles || 0), 0);
      return {
        ...definition,
        parts: selected,
        triangles
      };
    })
    .filter((group) => group.parts.length > 0);
}

function renderArmorSetPlan(host, parts) {
  const groups = buildArmorSetPlanGroups(parts);
  if (!groups.length) {
    return;
  }

  const panel = document.createElement("div");
  panel.className = "armor-set-plan";

  const title = document.createElement("div");
  title.className = "raw-model-analysis-title";
  const totalParts = groups.reduce((sum, group) => sum + group.parts.length, 0);
  title.textContent = `План разборки брони: ${totalParts} частей по ${groups.length} игровым зонам`;
  panel.appendChild(title);

  const grid = document.createElement("div");
  grid.className = "armor-set-plan-grid";
  for (const group of groups) {
    const card = document.createElement("div");
    card.className = "armor-set-plan-card";

    const cardTitle = document.createElement("div");
    cardTitle.className = "vehicle-profile-card-title";
    cardTitle.textContent = group.title;
    card.appendChild(cardTitle);

    const meta = document.createElement("div");
    meta.className = "raw-model-part-meta";
    meta.textContent = `${group.target} | ${group.parts.length} частей | ${formatCompactInteger(group.triangles)} tris`;
    card.appendChild(meta);

    appendProfileChips(card, group.parts.map((part) => part.name || "part"), "profile-chip", 10);

    const note = document.createElement("div");
    note.className = "raw-model-part-recommendation";
    note.textContent = group.note;
    card.appendChild(note);

    grid.appendChild(card);
  }

  panel.appendChild(grid);
  host.appendChild(panel);
}

function isArmorSetRawModel(model) {
  const parts = Array.isArray(model?.parts) ? model.parts : [];
  return parts.filter((part) => String(part?.role || "").toLowerCase().startsWith("armor-")).length >= 2;
}

function selectedRawModelImport() {
  const rawModels = state.modding.rawModelImports || [];
  const select = el("rawModelCookSource");
  if (!rawModels.length) {
    return null;
  }

  return rawModels.find((model) => model.sourceRelativePath === select?.value) || rawModels[0];
}

function clearArmorSetPlanPanel() {
  state.modding.armorSetPlan = null;
  state.modding.armorSetPlanKey = "";
  state.modding.armorSetPlanLoading = false;
  state.modding.armorSetPlanCookingKey = "";
  renderArmorSetPlanPanel();
}

function clearVehicleProfilePanel() {
  state.modding.vehicleProfile = null;
  state.modding.vehicleProfileAssetId = "";
  state.modding.vehicleProfileLoading = false;
  renderVehicleProfilePanel();
  clearVehicleModulePlanPanel();
}

function clearVehicleModulePlanPanel() {
  state.modding.vehicleModulePlan = null;
  state.modding.vehicleModulePlanKey = "";
  state.modding.vehicleModulePlanLoading = false;
  state.modding.vehicleModulePlanCookingKey = "";
  renderVehicleModulePlanPanel();
}

async function loadArmorSetPlanForCurrentSelection() {
  const rawModel = selectedRawModelImport();
  if (!rawModel?.sourceRelativePath || !isArmorSetRawModel(rawModel)) {
    clearArmorSetPlanPanel();
    return;
  }

  const key = rawModel.sourceRelativePath;
  if (state.modding.armorSetPlanLoading && state.modding.armorSetPlanKey === key) {
    return;
  }

  state.modding.armorSetPlanLoading = true;
  state.modding.armorSetPlanKey = key;
  state.modding.armorSetPlan = null;
  renderArmorSetPlanPanel();

  try {
    const plan = await api(`/api/modding/armor-set-plan?rawSourceRelativePath=${encodeURIComponent(rawModel.sourceRelativePath)}`);
    const currentRaw = selectedRawModelImport();
    if ((currentRaw?.sourceRelativePath || "") !== key) {
      return;
    }

    state.modding.armorSetPlan = plan;
  } catch (error) {
    state.modding.armorSetPlan = {
      ok: false,
      error: error?.message || "План комплекта брони не удалось построить.",
      entries: [],
      warnings: []
    };
  } finally {
    if (state.modding.armorSetPlanKey === key) {
      state.modding.armorSetPlanLoading = false;
      renderArmorSetPlanPanel();
    }
  }
}

function formatArmorSetModuleRole(role) {
  return {
    helmet: "шлем",
    torso: "бронежилет / торс",
    arms: "руки / плечи",
    legs: "ноги",
    boots: "ботинки / голени",
    hands: "перчатки / кисти"
  }[String(role || "").toLowerCase()] || String(role || "часть брони");
}

async function cookArmorSetPlanEntry(entry) {
  const rawModel = selectedRawModelImport();
  if (!rawModel?.sourceRelativePath || !entry?.targetAssetId) {
    setModelReplacementStatus("Выбери raw-модель комплекта брони и слот из плана.", true);
    return;
  }

  const key = `${entry.targetAssetId}|${entry.targetFieldPath || ""}`;
  state.modding.armorSetPlanCookingKey = key;
  renderArmorSetPlanPanel();
  setModelReplacementStatus(`Готовлю часть брони: ${entry.targetDisplayName || formatArmorSetModuleRole(entry.moduleRole)}...`);

  try {
    const result = await api("/api/modding/armor-set-cook", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        rawSourceRelativePath: rawModel.sourceRelativePath,
        targetAssetId: entry.targetAssetId,
        targetFieldPath: entry.targetFieldPath || ""
      })
    });

    const warnings = Array.isArray(result.warnings) ? result.warnings : [];
    if (result.ok === false) {
      const tail = result.unrealLogTail || result.blenderLogTail || "";
      const tailPreview = tail ? `\n${tail.split("\n").slice(-4).join("\n")}` : "";
      setModelReplacementStatus(`${result.error || "Часть брони не удалось приготовить."}${warnings.length ? `\n${warnings.slice(0, 4).join(" ")}` : ""}${tailPreview}`, true);
      return;
    }

    await fetchCustomVisualModels();
    const stagedItem = stageSuggestedAssetEditFromCook(result.suggestedEdit, entry);
    refreshModelReplacementWizard();
    const stagedNote = stagedItem
      ? "Правки safe-полей уже добавлены в мод."
      : "Cooked mesh добавлен в список моделей; поле можно выбрать вручную.";
    setModelReplacementStatus(`Часть брони приготовлена. ${stagedNote} ${warnings.slice(0, 3).join(" ")}`);
  } finally {
    if (state.modding.armorSetPlanCookingKey === key) {
      state.modding.armorSetPlanCookingKey = "";
      renderArmorSetPlanPanel();
    }
  }
}

async function cookArmorSetPlanBatch() {
  const rawModel = selectedRawModelImport();
  const plan = state.modding.armorSetPlan;
  if (!rawModel?.sourceRelativePath || !plan) {
    setModelReplacementStatus("Выбери raw-модель комплекта брони для batch cook.", true);
    return;
  }

  const autoCount = (plan.entries || []).filter((entry) => entry.canAutoCook).length;
  if (!autoCount) {
    setModelReplacementStatus("В плане брони нет безопасных слотов для автоматической подготовки.", true);
    return;
  }

  state.modding.armorSetPlanBatchCooking = true;
  renderArmorSetPlanPanel();
  setModelReplacementStatus(`Готовлю комплект брони по слотам (${autoCount}). Blender/UE4.27 будут работать несколько минут...`);

  try {
    const result = await api("/api/modding/armor-set-cook-batch", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        rawSourceRelativePath: rawModel.sourceRelativePath,
        maxModules: Math.min(autoCount, 6)
      })
    });

    const warnings = Array.isArray(result.warnings) ? result.warnings : [];
    if (result.ok === false && !Array.isArray(result.items)) {
      setModelReplacementStatus(`${result.error || "Комплект брони не удалось подготовить."}${warnings.length ? ` ${warnings.slice(0, 4).join(" ")}` : ""}`, true);
      return;
    }

    await fetchCustomVisualModels();
    let stagedCount = 0;
    for (const item of result.items || []) {
      if (stageSuggestedAssetEditFromCook(item.suggestedEdit, {
        targetRelativePath: item.targetRelativePath,
        targetDisplayName: item.targetDisplayName
      })) {
        stagedCount += 1;
      }
    }

    refreshModelReplacementWizard();
    const successCount = (result.items || []).filter((item) => item.ok).length;
    const totalCount = (result.items || []).length;
    setModelReplacementStatus(`Комплект брони подготовлен: ${successCount}/${totalCount} слотов, staged edits: ${stagedCount}. ${warnings.slice(0, 3).join(" ")}`);
  } finally {
    state.modding.armorSetPlanBatchCooking = false;
    renderArmorSetPlanPanel();
  }
}

function renderArmorSetPlanPanel() {
  const host = el("armorSetPlanPanel");
  if (!host) {
    return;
  }

  host.innerHTML = "";
  if (state.modding.armorSetPlanLoading) {
    host.hidden = false;
    const loading = document.createElement("div");
    loading.className = "vehicle-profile-title";
    loading.textContent = "Строю план комплекта брони по игровым слотам...";
    host.appendChild(loading);
    return;
  }

  const plan = state.modding.armorSetPlan;
  if (!plan) {
    host.hidden = true;
    return;
  }

  host.hidden = false;
  const title = document.createElement("div");
  title.className = "vehicle-profile-title";
  title.textContent = `Комплект брони: ${selectedRawModelImport()?.name || "raw model"}`;
  host.appendChild(title);

  const entries = Array.isArray(plan.entries) ? plan.entries : [];
  const autoCount = entries.filter((entry) => entry.canAutoCook).length;
  const summary = document.createElement("div");
  summary.className = "vehicle-profile-summary";
  summary.textContent = `слотов: ${entries.length} | можно подготовить автоматически: ${autoCount}`;
  host.appendChild(summary);

  if (plan.ok === false) {
    appendProfileList(host, "Ошибка", [plan.error || "План комплекта брони не удалось построить."], 1, "vehicle-profile-warning");
    appendProfileList(host, "Что сделать", plan.warnings || [], 4, "vehicle-profile-warning");
    return;
  }

  appendProfileList(host, "Как программа разложит сет", plan.nextSteps || [], 4);
  appendProfileList(host, "Предупреждения", plan.warnings || [], 4, "vehicle-profile-warning");

  if (autoCount > 0) {
    const actions = document.createElement("div");
    actions.className = "armor-set-plan-actions";
    const batchButton = document.createElement("button");
    batchButton.type = "button";
    batchButton.textContent = state.modding.armorSetPlanBatchCooking
      ? "Готовлю комплект..."
      : `Подготовить весь сет (${Math.min(autoCount, 6)})`;
    batchButton.disabled = state.modding.armorSetPlanBatchCooking || Boolean(state.modding.armorSetPlanCookingKey);
    batchButton.title = "Готовит шлем, жилет, руки, ноги и ботинки как отдельные cooked assets и добавляет safe staged edits.";
    batchButton.addEventListener("click", () => {
      cookArmorSetPlanBatch().catch(showError);
    });
    actions.appendChild(batchButton);
    host.appendChild(actions);
  }

  const grid = document.createElement("div");
  grid.className = "armor-set-plan-grid";
  for (const entry of entries) {
    const card = document.createElement("div");
    card.className = `armor-set-plan-card armor-set-plan-entry ${entry.canAutoCook ? "can-cook" : "needs-review"}`;

    const cardTitle = document.createElement("div");
    cardTitle.className = "vehicle-profile-card-title";
    cardTitle.textContent = formatArmorSetModuleRole(entry.moduleRole);
    card.appendChild(cardTitle);

    const meta = document.createElement("div");
    meta.className = "raw-model-part-meta";
    meta.textContent = `${entry.targetDisplayName || entry.targetRelativePath || "slot"} | ${entry.targetMeshKind || "mesh"} | ${formatCompactInteger(entry.rawTriangleCount)} tris -> ${formatCompactInteger(entry.targetTriangleCount)} tris`;
    card.appendChild(meta);

    if (entry.targetFieldLabel || entry.targetFieldPath) {
      const field = document.createElement("div");
      field.className = "vehicle-profile-fields";
      field.textContent = `${entry.targetFieldLabel || "visual field"} ${entry.targetFieldPath ? `(${entry.targetFieldPath})` : ""}`;
      card.appendChild(field);
    }

    appendProfileChips(card, entry.rawPartNames || [], "profile-chip", 10);

    const recommendation = document.createElement("div");
    recommendation.className = "raw-model-part-recommendation";
    recommendation.textContent = entry.recommendation || "";
    card.appendChild(recommendation);

    if (entry.canAutoCook) {
      const actions = document.createElement("div");
      actions.className = "armor-set-plan-actions";
      const cookButton = document.createElement("button");
      cookButton.type = "button";
      const key = `${entry.targetAssetId}|${entry.targetFieldPath || ""}`;
      const isCooking = state.modding.armorSetPlanCookingKey === key;
      cookButton.textContent = isCooking ? "Готовлю..." : "Подготовить слот";
      cookButton.disabled = isCooking || Boolean(state.modding.armorSetPlanCookingKey) || state.modding.armorSetPlanBatchCooking;
      cookButton.addEventListener("click", () => {
        cookArmorSetPlanEntry(entry).catch(showError);
      });
      actions.appendChild(cookButton);
      card.appendChild(actions);
    }

    grid.appendChild(card);
  }

  host.appendChild(grid);
}

function isVehicleAssetContext() {
  const haystack = [
    state.modding.selectedAssetId,
    state.modding.selectedAsset?.relativePath,
    state.modding.currentSchema?.relativePath,
    state.modding.currentSchema?.categoryId,
    state.modding.currentSchema?.categoryName
  ].filter(Boolean).join(" ").replace(/\\/g, "/").toLowerCase();

  return VEHICLE_ADAPTER_CLIENT_VISIBLE && isVehicleLikeContextText(haystack);
}

function isVehicleLikeContextText(haystack) {
  return haystack.includes("/vehicles/")
    || haystack.includes("vehicle")
    || haystack.includes("airplane")
    || haystack.includes("duster")
    || haystack.includes("kinglet");
}

async function loadVehicleProfileForCurrentAsset() {
  const assetId = state.modding.selectedAssetId;
  if (!VEHICLE_ADAPTER_CLIENT_VISIBLE || !assetId || !isVehicleAssetContext()) {
    clearVehicleProfilePanel();
    return;
  }

  state.modding.vehicleProfileLoading = true;
  state.modding.vehicleProfileAssetId = assetId;
  state.modding.vehicleProfile = null;
  renderVehicleProfilePanel();

  const profile = await api(`/api/modding/vehicle-profile?assetId=${encodeURIComponent(assetId)}`);
  if (state.modding.selectedAssetId !== assetId) {
    return;
  }

  state.modding.vehicleProfile = profile;
  state.modding.vehicleProfileAssetId = assetId;
  state.modding.vehicleProfileLoading = false;
  renderVehicleProfilePanel();
  loadVehicleModulePlanForCurrentSelection().catch(showError);
}

async function loadVehicleModulePlanForCurrentSelection() {
  const assetId = state.modding.selectedAssetId;
  const rawModel = selectedRawModelImport();
  if (!assetId || !rawModel?.sourceRelativePath || !isVehicleAssetContext()) {
    clearVehicleModulePlanPanel();
    return;
  }

  const key = `${assetId}|${rawModel.sourceRelativePath}`;
  if (state.modding.vehicleModulePlanLoading && state.modding.vehicleModulePlanKey === key) {
    return;
  }

  state.modding.vehicleModulePlanLoading = true;
  state.modding.vehicleModulePlanKey = key;
  state.modding.vehicleModulePlan = null;
  renderVehicleModulePlanPanel();

  try {
    const plan = await api(`/api/modding/vehicle-module-plan?assetId=${encodeURIComponent(assetId)}&rawSourceRelativePath=${encodeURIComponent(rawModel.sourceRelativePath)}`);
    const currentRaw = selectedRawModelImport();
    const currentKey = `${state.modding.selectedAssetId}|${currentRaw?.sourceRelativePath || ""}`;
    if (currentKey !== key) {
      return;
    }

    state.modding.vehicleModulePlan = plan;
  } catch (error) {
    state.modding.vehicleModulePlan = {
      ok: false,
      error: error?.message || "План модулей не удалось построить.",
      displayName: state.modding.selectedAsset?.relativePath || "vehicle",
      entries: [],
      warnings: []
    };
  } finally {
    if (state.modding.vehicleModulePlanKey === key) {
      state.modding.vehicleModulePlanLoading = false;
      renderVehicleModulePlanPanel();
    }
  }
}

function appendProfileChips(host, items, className = "profile-chip", limit = 24) {
  const values = (Array.isArray(items) ? items : [])
    .map((item) => String(item || "").trim())
    .filter(Boolean)
    .slice(0, limit);
  if (!values.length) {
    return;
  }

  const chipHost = document.createElement("div");
  chipHost.className = "profile-chip-list";
  for (const value of values) {
    const chip = document.createElement("span");
    chip.className = className;
    chip.textContent = value;
    chip.title = value;
    chipHost.appendChild(chip);
  }
  host.appendChild(chipHost);
}

function appendProfileList(host, titleText, items, limit = 6, className = "") {
  const values = (Array.isArray(items) ? items : [])
    .map((item) => String(item || "").trim())
    .filter(Boolean)
    .slice(0, limit);
  if (!values.length) {
    return;
  }

  const block = document.createElement("div");
  block.className = className || "vehicle-profile-note";
  const title = document.createElement("strong");
  title.textContent = titleText;
  block.appendChild(title);
  const list = document.createElement("ul");
  for (const value of values) {
    const item = document.createElement("li");
    item.textContent = value;
    list.appendChild(item);
  }
  block.appendChild(list);
  host.appendChild(block);
}

function renderVehicleProfilePanel() {
  const host = el("vehicleProfilePanel");
  if (!host) {
    return;
  }

  host.innerHTML = "";
  const profile = state.modding.vehicleProfile;
  if (state.modding.vehicleProfileLoading) {
    host.hidden = false;
    const loading = document.createElement("div");
    loading.className = "vehicle-profile-title";
    loading.textContent = "Читаю модульный профиль транспорта...";
    host.appendChild(loading);
    return;
  }

  if (!profile) {
    host.hidden = true;
    return;
  }

  host.hidden = false;
  const title = document.createElement("div");
  title.className = "vehicle-profile-title";
  title.textContent = `Профиль транспорта: ${profile.displayName || profile.relativePath || "asset"}`;
  host.appendChild(title);

  const summary = document.createElement("div");
  summary.className = "vehicle-profile-summary";
  const assets = Array.isArray(profile.assets) ? profile.assets : [];
  const links = Array.isArray(profile.links) ? profile.links : [];
  summary.textContent = `${profile.profileKind || "vehicle"} | модулей: ${assets.length} | связей: ${links.length}`;
  host.appendChild(summary);

  if (profile.ok === false) {
    appendProfileList(host, "Ошибка", [profile.error || "Профиль не удалось построить."], 1, "vehicle-profile-warning");
    return;
  }

  appendProfileList(host, "Что важно перед заменой", profile.recommendations || [], 6);
  appendProfileList(host, "Предупреждения", profile.warnings || [], 4, "vehicle-profile-warning");

  if (Array.isArray(profile.requiredSockets) && profile.requiredSockets.length) {
    const socketsTitle = document.createElement("strong");
    socketsTitle.textContent = "Обязательные сокеты";
    host.appendChild(socketsTitle);
    appendProfileChips(host, profile.requiredSockets, "profile-chip profile-chip-socket", 40);
  }

  if (Array.isArray(profile.materialReferences) && profile.materialReferences.length) {
    const materialTitle = document.createElement("strong");
    materialTitle.textContent = "Материалы из игры";
    host.appendChild(materialTitle);
    appendProfileChips(host, profile.materialReferences, "profile-chip profile-chip-material", 20);
  }

  const grid = document.createElement("div");
  grid.className = "vehicle-profile-grid";
  for (const asset of assets.slice(0, 24)) {
    const card = document.createElement("div");
    card.className = "vehicle-profile-card";

    const cardTitle = document.createElement("div");
    cardTitle.className = "vehicle-profile-card-title";
    cardTitle.textContent = asset.displayName || asset.relativePath || "asset";
    card.appendChild(cardTitle);

    const role = document.createElement("div");
    role.className = "small muted";
    role.textContent = `${asset.role || "module"} | visual ${asset.visualFieldCount || 0} | query ${asset.queryFieldCount || 0} | sockets ${asset.socketFieldCount || 0}`;
    card.appendChild(role);

    appendProfileChips(card, asset.requiredSockets || [], "profile-chip profile-chip-socket", 8);
    appendProfileList(card, "Риски", asset.warnings || [], 2, "vehicle-profile-warning compact");
    const keyFields = (Array.isArray(asset.keyFields) ? asset.keyFields : []).slice(0, 5);
    if (keyFields.length) {
      const fields = document.createElement("div");
      fields.className = "vehicle-profile-fields";
      for (const field of keyFields) {
        const row = document.createElement("div");
        row.textContent = `${field.kind || "field"}: ${field.label || field.fieldPath || ""}`;
        row.title = field.currentDisplayValue || field.currentValue || "";
        fields.appendChild(row);
      }
      card.appendChild(fields);
    }

    grid.appendChild(card);
  }
  host.appendChild(grid);
}

function formatVehicleModuleRole(role) {
  return {
    "vehicle-root": "root actor",
    chassis: "chassis",
    engine: "engine/propeller",
    wing: "wing/airfoil",
    "tail-control": "tail/rudder",
    "landing-gear": "landing gear",
    "weapon-mount": "weapon mount",
    "seat-driver": "driver seat",
    "seat-passenger": "passenger seat"
  }[String(role || "").toLowerCase()] || String(role || "module");
}

function formatVehicleModuleSafety(safety) {
  return {
    "candidate-static-visual": "можно готовить StaticMesh",
    "blocked-skeletal-contract": "блок: skeleton/ABP/sockets",
    "blocked-query-proxy": "блок: нужен query proxy",
    "mount-slot-plan": "план посадки",
    "needs-split": "нужно разделить модель",
    "needs-optimization": "нужны LOD/упрощение",
    "needs-field-analysis": "нужно дочитать attachment",
    "manual-review": "ручная проверка"
  }[String(safety || "").toLowerCase()] || String(safety || "ручная проверка");
}

async function cookVehicleModulePlanEntry(entry) {
  const rawModel = selectedRawModelImport();
  if (!rawModel?.sourceRelativePath || !entry?.targetAssetId) {
    setModelReplacementStatus("Выбери raw-модель и модуль из плана.", true);
    return;
  }

  const key = `${entry.targetAssetId}|${entry.targetFieldPath || ""}`;
  state.modding.vehicleModulePlanCookingKey = key;
  renderVehicleModulePlanPanel();
  setModelReplacementStatus(`Готовлю модуль ${entry.targetDisplayName || entry.targetRelativePath || "vehicle"} через Blender/UE4.27...`);

  try {
    const result = await api("/api/modding/vehicle-module-cook", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        assetId: state.modding.selectedAssetId,
        rawSourceRelativePath: rawModel.sourceRelativePath,
        targetAssetId: entry.targetAssetId,
        targetFieldPath: entry.targetFieldPath || "",
        materialReference: (entry.materialReferences || [])[0] || ""
      })
    });

    const warnings = Array.isArray(result.warnings) ? result.warnings : [];
    if (result.ok === false) {
      const tail = result.unrealLogTail || result.blenderLogTail || "";
      const tailPreview = tail ? `\n${tail.split("\n").slice(-4).join("\n")}` : "";
      setModelReplacementStatus(`${result.error || "Модуль не удалось приготовить."}${warnings.length ? `\n${warnings.slice(0, 4).join(" ")}` : ""}${tailPreview}`, true);
      return;
    }

    await fetchCustomVisualModels();
    const stagedItem = stageSuggestedAssetEditFromCook(result.suggestedEdit, entry);
    refreshModelReplacementWizard();
    const stagedNote = stagedItem
      ? "Правка attachment уже добавлена в мод."
      : "Cooked mesh добавлен в список моделей; поле attachment нужно выбрать вручную.";
    setModelReplacementStatus(`Модуль приготовлен. ${stagedNote} ${warnings.slice(0, 3).join(" ")}`);
  } finally {
    if (state.modding.vehicleModulePlanCookingKey === key) {
      state.modding.vehicleModulePlanCookingKey = "";
      renderVehicleModulePlanPanel();
    }
  }
}

async function cookVehicleModulePlanBatch() {
  const rawModel = selectedRawModelImport();
  const plan = state.modding.vehicleModulePlan;
  if (!rawModel?.sourceRelativePath || !state.modding.selectedAssetId || !plan) {
    setModelReplacementStatus("Выбери транспортный ассет и raw-модель для batch cook.", true);
    return;
  }

  const autoCount = (plan.entries || []).filter((entry) => entry.canAutoCook).length;
  if (!autoCount) {
    setModelReplacementStatus("В плане нет безопасных StaticMesh-модулей для batch cook.", true);
    return;
  }

  state.modding.vehicleModulePlanBatchCooking = true;
  renderVehicleModulePlanPanel();
  setModelReplacementStatus(`Готовлю безопасные модули транспорта (${autoCount}). Это может занять несколько минут...`);

  try {
    const result = await api("/api/modding/vehicle-module-cook-batch", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        assetId: state.modding.selectedAssetId,
        rawSourceRelativePath: rawModel.sourceRelativePath,
        maxModules: Math.min(autoCount, 8)
      })
    });

    const warnings = Array.isArray(result.warnings) ? result.warnings : [];
    if (result.ok === false && !Array.isArray(result.items)) {
      setModelReplacementStatus(`${result.error || "Batch cook не удалось выполнить."}${warnings.length ? ` ${warnings.slice(0, 4).join(" ")}` : ""}`, true);
      return;
    }

    await fetchCustomVisualModels();
    let stagedCount = 0;
    for (const item of result.items || []) {
      if (stageSuggestedAssetEditFromCook(item.suggestedEdit, {
        targetRelativePath: item.targetRelativePath,
        targetDisplayName: item.targetDisplayName
      })) {
        stagedCount += 1;
      }
    }

    refreshModelReplacementWizard();
    const successCount = (result.items || []).filter((item) => item.ok).length;
    const totalCount = (result.items || []).length;
    setModelReplacementStatus(`Batch cook транспорта готов: ${successCount}/${totalCount} модулей, staged edits: ${stagedCount}. ${warnings.slice(0, 3).join(" ")}`);
  } finally {
    state.modding.vehicleModulePlanBatchCooking = false;
    renderVehicleModulePlanPanel();
  }
}

function stageSuggestedAssetEditFromCook(suggestedEdit, entry) {
  if (!suggestedEdit?.assetId) {
    return null;
  }

  const edits = Array.isArray(suggestedEdit.edits)
    ? suggestedEdit.edits
        .filter((field) => field?.fieldPath && field.value)
        .map((field) => ({
          fieldPath: String(field.fieldPath),
          value: String(field.value)
        }))
    : [];
  const listEdits = Array.isArray(suggestedEdit.listEdits)
    ? suggestedEdit.listEdits.map((operation) => ({ ...operation }))
    : [];
  if (!edits.length && !listEdits.length) {
    return null;
  }

  const existing = state.modding.stagedByAssetId.get(suggestedEdit.assetId) || null;
  const stagedItem = {
    assetId: suggestedEdit.assetId,
    relativePath: entry?.targetRelativePath || existing?.relativePath || suggestedEdit.assetId,
    displayName: entry?.targetDisplayName || existing?.displayName || suggestedEdit.assetId,
    sourceMode: existing?.sourceMode || "auto",
    companionMode: existing?.companionMode || "auto",
    edits: existing ? mergeFieldEdits(existing.edits, edits) : edits,
    listEdits: existing ? [...(existing.listEdits || []), ...listEdits] : listEdits
  };

  state.modding.stagedByAssetId.set(stagedItem.assetId, stagedItem);
  renderStagedEdits();
  updateModAssetMeta();
  return stagedItem;
}

function renderVehicleModulePlanPanel() {
  const host = el("vehicleModulePlanPanel");
  if (!host) {
    return;
  }

  host.innerHTML = "";
  if (state.modding.vehicleModulePlanLoading) {
    host.hidden = false;
    const loading = document.createElement("div");
    loading.className = "vehicle-profile-title";
    loading.textContent = "Собираю план модулей транспорта для выбранной raw-модели...";
    host.appendChild(loading);
    return;
  }

  const plan = state.modding.vehicleModulePlan;
  if (!plan) {
    host.hidden = true;
    return;
  }

  host.hidden = false;
  const title = document.createElement("div");
  title.className = "vehicle-profile-title";
  title.textContent = `План модульной замены: ${plan.displayName || "vehicle"} + ${selectedRawModelImport()?.name || "raw model"}`;
  host.appendChild(title);

  const entries = Array.isArray(plan.entries) ? plan.entries : [];
  const autoCount = entries.filter((entry) => entry.canAutoCook).length;
  const blockedCount = entries.filter((entry) => String(entry.safetyLevel || "").startsWith("blocked")).length;
  const summary = document.createElement("div");
  summary.className = "vehicle-profile-summary";
  summary.textContent = `${plan.profileKind || "vehicle"} | модулей: ${entries.length} | auto-cook кандидатов: ${autoCount} | заблокировано: ${blockedCount}`;
  host.appendChild(summary);

  if (plan.ok === false) {
    appendProfileList(host, "Ошибка", [plan.error || "План не удалось построить."], 1, "vehicle-profile-warning");
    return;
  }

  appendProfileList(host, "Следующие шаги", plan.nextSteps || [], 6);
  appendProfileList(host, "Предупреждения", plan.warnings || [], 5, "vehicle-profile-warning");

  if (autoCount > 0) {
    const batchActions = document.createElement("div");
    batchActions.className = "vehicle-module-plan-actions";
    const batchButton = document.createElement("button");
    batchButton.type = "button";
    batchButton.textContent = state.modding.vehicleModulePlanBatchCooking
      ? "Готовлю безопасные..."
      : `Подготовить безопасные (${Math.min(autoCount, 8)})`;
    batchButton.disabled = state.modding.vehicleModulePlanBatchCooking || Boolean(state.modding.vehicleModulePlanCookingKey);
    batchButton.title = "Автоматически готовит только StaticMesh-модули, которые plan пометил как безопасные для auto-cook, и кладёт staged edits в мод.";
    batchButton.addEventListener("click", () => {
      cookVehicleModulePlanBatch().catch(showError);
    });
    batchActions.appendChild(batchButton);
    host.appendChild(batchActions);
  }

  const grid = document.createElement("div");
  grid.className = "vehicle-module-plan-grid";
  for (const entry of entries.slice(0, 32)) {
    const card = document.createElement("div");
    card.className = `vehicle-module-plan-card safety-${String(entry.safetyLevel || "manual-review").toLowerCase()}`;

    const cardTitle = document.createElement("div");
    cardTitle.className = "vehicle-profile-card-title";
    cardTitle.textContent = entry.targetDisplayName || entry.targetRelativePath || "module";
    card.appendChild(cardTitle);

    const meta = document.createElement("div");
    meta.className = "small muted";
    meta.textContent = `${formatVehicleModuleRole(entry.moduleRole)} | ${formatVehicleModuleSafety(entry.safetyLevel)} | ${entry.targetMeshKind || "unknown"}`;
    card.appendChild(meta);

    if (entry.targetFieldLabel || entry.targetFieldPath) {
      const field = document.createElement("div");
      field.className = "vehicle-profile-fields";
      field.textContent = `${entry.targetFieldLabel || "field"} ${entry.targetFieldPath ? `(${entry.targetFieldPath})` : ""}`;
      card.appendChild(field);
    }

    appendProfileChips(card, entry.rawPartNames || [], "profile-chip", 8);
    if (entry.rawTriangleCount) {
      const tris = document.createElement("div");
      tris.className = "small muted";
      const triangleBudget = Number(entry.targetTriangleCount || 0);
      tris.textContent = triangleBudget > 0
        ? `Raw parts: ${formatCompactInteger(entry.rawTriangleCount)} tris -> budget ${formatCompactInteger(triangleBudget)} | цель ${Math.round(Number(entry.targetLongestCm || 0))} см`
        : `Raw parts: ${formatCompactInteger(entry.rawTriangleCount)} tris | цель ${Math.round(Number(entry.targetLongestCm || 0))} см`;
      card.appendChild(tris);
    }
    appendProfileChips(card, entry.requiredSockets || [], "profile-chip profile-chip-socket", 6);
    appendProfileChips(card, entry.materialReferences || [], "profile-chip profile-chip-material", 4);

    const recommendation = document.createElement("div");
    recommendation.className = "raw-model-part-recommendation";
    recommendation.textContent = entry.recommendation || entry.replacementStrategy || "";
    card.appendChild(recommendation);

    if (entry.canAutoCook) {
      const actions = document.createElement("div");
      actions.className = "vehicle-module-plan-actions";
      const cookButton = document.createElement("button");
      cookButton.type = "button";
      const key = `${entry.targetAssetId}|${entry.targetFieldPath || ""}`;
      const isCooking = state.modding.vehicleModulePlanCookingKey === key;
      cookButton.textContent = isCooking ? "Готовлю..." : "Подготовить модуль";
      cookButton.disabled = isCooking || Boolean(state.modding.vehicleModulePlanCookingKey) || state.modding.vehicleModulePlanBatchCooking;
      cookButton.title = entry.replacementStrategy || "Blender/UE4.27 приготовят только выбранные raw parts как отдельный StaticMesh.";
      cookButton.addEventListener("click", () => {
        cookVehicleModulePlanEntry(entry).catch(showError);
      });
      actions.appendChild(cookButton);
      card.appendChild(actions);
    }

    grid.appendChild(card);
  }

  host.appendChild(grid);
}

function renderRawModelAnalysisPanel() {
  const host = el("rawModelAnalysisPanel");
  if (!host) {
    return;
  }

  const model = selectedRawModelImport();
  if (!model) {
    host.hidden = true;
    host.innerHTML = "";
    renderStudioFlowBar();
    return;
  }

  host.hidden = false;
  host.innerHTML = "";
  const title = document.createElement("div");
  title.className = "raw-model-analysis-title";
  const bounds = formatModelBounds(model.bounds);
  title.textContent = bounds
    ? `Анализ модели: ${model.name || "raw model"} (${model.format || "MODEL"}, ${bounds})`
    : `Анализ модели: ${model.name || "raw model"} (${model.format || "MODEL"})`;
  host.appendChild(title);

  appendProfileList(host, "Автоподготовка", model.adaptationHints || [], 4, "raw-model-analysis-note");

  const parts = Array.isArray(model.parts) ? model.parts : [];
  if (!parts.length) {
    const empty = document.createElement("div");
    empty.className = "muted small";
    empty.textContent = "Части модели пока не распознаны. Программа пытается разложить FBX/OBJ/GLTF/DAE/STL/PLY на экипировку, NPC/body, корпус, двигатель, крылья, оружие и query/collision.";
    host.appendChild(empty);
    return;
  }

  renderArmorSetPlan(host, parts);

  const table = document.createElement("div");
  table.className = "raw-model-parts";
  for (const part of parts.slice(0, 32)) {
    const row = document.createElement("div");
    row.className = "raw-model-part-row";

    const name = document.createElement("div");
    name.className = "raw-model-part-name";
    name.textContent = part.name || "part";
    row.appendChild(name);

    const meta = document.createElement("div");
    meta.className = "raw-model-part-meta";
    meta.textContent = `${formatRawModelPartRole(part.role)} | ${formatCompactInteger(part.triangles)} tris | ${formatModelBounds(part.bounds)}`;
    row.appendChild(meta);

    const recommendation = document.createElement("div");
    recommendation.className = "raw-model-part-recommendation";
    recommendation.textContent = part.recommendation || "";
    row.appendChild(recommendation);

    table.appendChild(row);
  }
  host.appendChild(table);
  renderStudioFlowBar();
}

function setCustomVisualImportStatus(text, isError = false) {
  const status = el("customVisualImportStatus");
  if (!status) {
    return;
  }

  status.textContent = text || "";
  status.classList.toggle("status-error", Boolean(isError));
}

async function importCustomVisualAssets() {
  const fileInput = el("customVisualFiles");
  const folderInput = el("customVisualFolder");
  const files = [
    ...Array.from(fileInput?.files || []),
    ...Array.from(folderInput?.files || [])
  ];
  if (!files.length) {
    setCustomVisualImportStatus("Выбери cooked UE-файлы или raw-модель для импорта.", true);
    return;
  }

  const form = new FormData();
  for (const file of files) {
    form.append("files", file, file.webkitRelativePath || file.name);
  }

  setCustomVisualImportStatus("Импортирую...");
  const result = await api("/api/custom-visual-assets/import", {
    method: "POST",
    body: form
  });

  const importedCount = Number(result.importedFileCount || 0);
  const assets = Array.isArray(result.assets) ? result.assets : [];
  const rawModels = Array.isArray(result.rawModels) ? result.rawModels : [];
  const warnings = Array.isArray(result.warnings) ? result.warnings : [];
  if (rawModels.length) {
    const keyForRawModel = (model) => model.sourceRelativePath || `${model.name || ""}|${model.format || ""}`;
    const merged = new Map((state.modding.rawModelImports || []).map((model) => [keyForRawModel(model), model]));
    for (const model of rawModels) {
      merged.set(keyForRawModel(model), model);
    }
    state.modding.rawModelImports = Array.from(merged.values());
  }
  const parts = [];

  if (result.ok === false) {
    parts.push(result.error || "Импорт не выполнен.");
  } else {
    parts.push(`Импортировано файлов: ${importedCount}. Распознано ассетов: ${assets.length}.`);
  }

  if (assets.length) {
    const preview = assets
      .slice(0, 4)
      .map((asset) => `${formatCustomVisualKind(asset.kind)}: ${asset.name}`)
      .join("; ");
    parts.push(preview);
  }

  if (rawModels.length) {
    const rawPreview = rawModels
      .slice(0, 3)
      .map((model) => {
        const bounds = formatModelBounds(model.bounds);
        return bounds
          ? `${model.format || "MODEL"}: ${model.name} (${bounds})`
          : `${model.format || "MODEL"}: ${model.name}`;
      })
      .join("; ");
    parts.push(`Сырые модели приняты как заготовки: ${rawPreview}`);
  }

  if (warnings.length) {
    parts.push(`Предупреждения: ${warnings.slice(0, 3).join(" ")}`);
  }

  setCustomVisualImportStatus(parts.join("\n"), result.ok === false);
  if (assets.length || rawModels.length) {
    await fetchCustomVisualModels();
    refreshModelReplacementWizard();
    renderRawModelAnalysisPanel();
    renderStudioFlowBar();
    loadArmorSetPlanForCurrentSelection().catch(showError);
    loadVehicleModulePlanForCurrentSelection().catch(showError);
  }
  if (fileInput) {
    fileInput.value = "";
  }
  if (folderInput) {
    folderInput.value = "";
  }
}

function isModelReplacementField(field) {
  const pickerKind = String(field?.referencePickerKind || "").toLowerCase();
  return field?.editable !== false
    && (pickerKind === "visual-static-mesh-object"
      || pickerKind === "visual-static-mesh-asset"
      || pickerKind === "visual-skeletal-mesh-object"
      || pickerKind === "visual-skeletal-mesh-asset");
}

function getModelReplacementFields() {
  const schema = state.modding.currentSchema;
  const fields = Array.isArray(schema?.fields) ? schema.fields : [];
  return fields.filter(isModelReplacementField);
}

function getModelReplacementContextText(field = null) {
  return [
    state.modding.selectedAssetId,
    state.modding.selectedAsset?.relativePath,
    state.modding.currentSchema?.relativePath,
    field?.sourceLabel,
    field?.label,
    field?.section,
    field?.currentValue,
    field?.currentDisplayValue
  ].filter(Boolean).join(" ").replace(/\\/g, "/").toLowerCase();
}

function isVehicleModelReplacementField(field) {
  const haystack = getModelReplacementContextText(field);
  return isVehicleLikeContextText(haystack);
}

function isWeaponModelReplacementField(field) {
  const haystack = getModelReplacementContextText(field);
  return haystack.includes("weapon")
    || haystack.includes("/weapons/")
    || haystack.includes("new_melee")
    || haystack.includes("melee")
    || haystack.includes("katana")
    || haystack.includes("machete")
    || haystack.includes("knife")
    || haystack.includes("sword")
    || haystack.includes("blade")
    || haystack.includes("ranged_weapons")
    || haystack.includes("shotgun")
    || haystack.includes("rifle")
    || haystack.includes("pistol")
    || haystack.includes("оруж");
}

function getVehicleModelReplacementIssue(field, options = {}) {
  if (!isVehicleModelReplacementField(field)) {
    return "";
  }

  if (!VEHICLE_ADAPTER_CLIENT_VISIBLE) {
    return "Замена моделей техники временно скрыта: vehicle adapter оставлен только для внутренней доработки.";
  }

  const haystack = getModelReplacementContextText(field);
  const pickerKind = String(field?.referencePickerKind || "").toLowerCase();
  if (haystack.includes("query mesh setup")) {
    return "QueryMesh транспорта отвечает за трассировку, collision и сервисные проверки. Его нельзя менять общей интернет-моделью; сначала нужен отдельный collision/query proxy.";
  }
  if (haystack.includes("destruction effect")) {
    return "Destruction meshes транспорта должны совпадать с damage regions и материалами оригинала. Общая замена здесь может ломать разрушение и физику.";
  }
  if (pickerKind.includes("skeletal")) {
    return "SkeletalMesh транспорта требует исходный skeleton/ABP/socket contract. Для Duster это отдельный vehicle profile, а не общий raw→skeletal cook.";
  }
  if (options.rawCook && getModelMaterialMode() !== "game") {
    return "Для транспортных StaticMesh-полей используй материал из SCUM/.mi: интернет-материалы могут не иметь shader map в cooked игре.";
  }
  return "";
}

function getModelReferenceValueForField(model, field) {
  const pickerKind = String(field?.referencePickerKind || "").toLowerCase();
  const wantsObject = pickerKind.endsWith("-object");
  return wantsObject ? model.objectReference : model.assetReference;
}

function getCompatibleModelOptionsForField(field) {
  const pickerKind = String(field?.referencePickerKind || "").toLowerCase();
  const wantsSkeletal = pickerKind.includes("skeletal");
  const wantsStatic = pickerKind.includes("static");
  return (state.modding.customVisualModels || []).filter((model) => {
    const kind = String(model.kind || "").toLowerCase();
    if (wantsSkeletal) {
      return kind === "skeletal-mesh";
    }
    if (wantsStatic) {
      return kind === "static-mesh";
    }
    return kind === "static-mesh" || kind === "skeletal-mesh";
  });
}

function getModelFitNumber(id, fallback = 0) {
  const value = Number(el(id)?.value ?? fallback);
  return Number.isFinite(value) ? value : fallback;
}

function syncModelTargetLongestSlider(source = "number") {
  const number = el("modelTargetLongestCm");
  const slider = el("modelTargetLongestSlider");
  if (!number || !slider) {
    return;
  }

  if (source === "slider") {
    number.value = slider.value;
    return;
  }

  const value = Math.max(Number(slider.min || 30), Math.min(Number(slider.max || 3000), getModelFitNumber("modelTargetLongestCm", 950)));
  slider.value = String(Math.round(value / 10) * 10);
}

const vehicleAdapterDefaults = {
  vehicleCollisionMode: "visual-query",
  vehicleQueryProxyLength: 96,
  vehicleQueryProxyWidth: 88,
  vehicleQueryProxyHeight: 92,
  vehicleSeatOffsetX: 0,
  vehicleSeatOffsetY: -12,
  vehicleSeatOffsetZ: 0,
  vehiclePassengerSeatOffsetX: 0,
  vehiclePassengerSeatOffsetY: -12,
  vehiclePassengerSeatOffsetZ: -5,
  vehicleEntryOffsetX: -60,
  vehicleEntryOffsetY: 100,
  vehicleEntryOffsetZ: 125
};

function setUntouchedControlValue(id, value) {
  const input = el(id);
  if (!input || input.dataset.userTouched === "1") {
    return;
  }

  input.value = String(value);
  input.dataset.adapterDefault = "1";
}

function syncVehicleAdapterControls(field = null) {
  const enabled = VEHICLE_ADAPTER_CLIENT_VISIBLE && Boolean(field) && (isVehicleAssetContext() || isVehicleModelReplacementField(field));
  document.querySelectorAll(".vehicle-adapter-control").forEach((node) => {
    node.hidden = !enabled;
  });

  if (!enabled) {
    return;
  }

  if (!state.modding.modelTargetLongestTouched) {
    setUntouchedControlValue("modelTargetLongestCm", inferModelTargetLongestCm(field));
    syncModelTargetLongestSlider("number");
  }

  setUntouchedControlValue("modelFitOffsetX", -500);
  setUntouchedControlValue("modelFitOffsetY", 0);
  setUntouchedControlValue("modelFitOffsetZ", 70);
  setUntouchedControlValue("modelFitPitch", 0);
  setUntouchedControlValue("modelFitYaw", 0);
  setUntouchedControlValue("modelFitRoll", 0);

  for (const [id, value] of Object.entries(vehicleAdapterDefaults)) {
    setUntouchedControlValue(id, value);
  }
}

function syncWeaponAdapterControls(field = null) {
  const enabled = Boolean(field) && isWeaponModelReplacementField(field);
  document.querySelectorAll(".weapon-adapter-control").forEach((node) => {
    node.hidden = !enabled;
  });

  if (!enabled) {
    return;
  }

  const haystack = getModelReplacementContextText(field);
  const isTwoHandBlade = haystack.includes("2h")
    || haystack.includes("katana")
    || haystack.includes("sword")
    || haystack.includes("twohand");
  setUntouchedControlValue("weaponGripAnchorPercent", isTwoHandBlade ? 45 : 55);
  setUntouchedControlValue("weaponGripDiameterCm", 0);
  setUntouchedControlValue("weaponGripBackReachCm", isTwoHandBlade ? 32 : 0);
  setUntouchedControlValue("weaponSecondHandShiftCm", isTwoHandBlade ? 24 : 0);
}

function isNpcCharacterModelReplacementField(field) {
  const haystack = getModelReplacementContextText(field);
  return haystack.includes("npc")
    || haystack.includes("zombie")
    || haystack.includes("puppet")
    || haystack.includes("/characters/zombies")
    || haystack.includes("/characters/npcs")
    || haystack.includes("skeletal")
    || haystack.includes("персонаж")
    || haystack.includes("зомби");
}

function isHelmetModelReplacementField(field) {
  const haystack = getModelReplacementContextText(field);
  return haystack.includes("helmet")
    || haystack.includes("headwear")
    || haystack.includes("upperhead")
    || haystack.includes("голов")
    || haystack.includes("шлем");
}

function isArmorModelReplacementField(field) {
  const haystack = getModelReplacementContextText(field);
  if (isHelmetModelReplacementField(field)) {
    return false;
  }

  return haystack.includes("vests_armor")
    || haystack.includes("torso_protection")
    || haystack.includes("armor_tactical")
    || haystack.includes("armor_police")
    || haystack.includes("body armor")
    || haystack.includes("body_armor")
    || haystack.includes("armored")
    || haystack.includes("armoured")
    || haystack.includes("militarypants")
    || haystack.includes("underwear_pants")
    || haystack.includes("jackets_coats")
    || haystack.includes("gloves")
    || haystack.includes("footwear")
    || haystack.includes("boots")
    || haystack.includes("pants")
    || haystack.includes("jacket")
    || haystack.includes("брон")
    || haystack.includes("жилет")
    || haystack.includes("куртк")
    || haystack.includes("штаны")
    || haystack.includes("перчат")
    || haystack.includes("ботин");
}

function isContainerModelReplacementField(field) {
  const haystack = getModelReplacementContextText(field);
  return haystack.includes("chest")
    || haystack.includes("crate")
    || haystack.includes("container")
    || haystack.includes("storage")
    || haystack.includes("wardrobe")
    || haystack.includes("locker")
    || haystack.includes("сундук")
    || haystack.includes("ящик")
    || haystack.includes("контейнер");
}

function isTwoHandMeleeModelReplacementField(field) {
  const haystack = getModelReplacementContextText(field);
  return haystack.includes("2h")
    || haystack.includes("katana")
    || haystack.includes("sword")
    || haystack.includes("twohand")
    || haystack.includes("двуруч");
}

function inferModelCookProfile(field = null) {
  if (!field) {
    return "generic";
  }
  if (isVehicleModelReplacementField(field)) {
    return "vehicle";
  }
  if (isTwoHandMeleeModelReplacementField(field)) {
    return "two-hand-melee";
  }
  if (isWeaponModelReplacementField(field)) {
    return "weapon";
  }
  if (isHelmetModelReplacementField(field)) {
    return "helmet";
  }
  if (isArmorModelReplacementField(field)) {
    return "armor";
  }
  if (isNpcCharacterModelReplacementField(field)) {
    return "npc";
  }
  if (isContainerModelReplacementField(field)) {
    return "container";
  }
  return "generic";
}

function setModelControlValue(id, value, markTouched = true) {
  const input = el(id);
  if (!input) {
    return;
  }
  input.value = String(value);
  if (markTouched) {
    input.dataset.userTouched = "1";
  }
}

function setModelMaterialModeValue(mode, markTouched = true) {
  const select = el("modelMaterialMode");
  if (!select) {
    return;
  }
  select.value = mode;
  if (markTouched) {
    select.dataset.userTouched = "1";
  }
  syncModelMaterialControls();
}

function applyModelCookProfile(profile, options = {}) {
  const markTouched = options.markTouched !== false;
  const field = getSelectedModelReplacementField();
  const resolved = profile === "auto" ? inferModelCookProfile(field) : profile;
  state.modding.modelProfilePreset = profile;
  state.modding.modelTargetLongestTouched = markTouched;

  const setNumber = (id, value) => setModelControlValue(id, value, markTouched);
  switch (resolved) {
    case "two-hand-melee":
      setNumber("modelTargetLongestCm", 115);
      setNumber("modelTriangleBudget", 0);
      setNumber("modelFitScale", 100);
      setNumber("modelFitOffsetX", 0);
      setNumber("modelFitOffsetY", 0);
      setNumber("modelFitOffsetZ", 0);
      setNumber("modelFitPitch", 0);
      setNumber("modelFitYaw", 0);
      setNumber("modelFitRoll", 0);
      setNumber("weaponGripAnchorPercent", 45);
      setNumber("weaponGripDiameterCm", 0);
      setNumber("weaponGripBackReachCm", 32);
      setNumber("weaponSecondHandShiftCm", 24);
      setModelMaterialModeValue("model", markTouched);
      break;
    case "helmet":
      setNumber("modelTargetLongestCm", 32);
      setNumber("modelTriangleBudget", 0);
      setNumber("modelFitScale", 100);
      setNumber("modelFitOffsetX", 0);
      setNumber("modelFitOffsetY", 0);
      setNumber("modelFitOffsetZ", 0);
      setNumber("modelFitPitch", 0);
      setNumber("modelFitYaw", 0);
      setNumber("modelFitRoll", 0);
      setModelMaterialModeValue("model", markTouched);
      break;
    case "armor":
      setNumber("modelTargetLongestCm", 85);
      setNumber("modelTriangleBudget", 10000);
      setNumber("modelFitScale", 100);
      setNumber("modelFitOffsetX", 0);
      setNumber("modelFitOffsetY", 0);
      setNumber("modelFitOffsetZ", 0);
      setNumber("modelFitPitch", 0);
      setNumber("modelFitYaw", 0);
      setNumber("modelFitRoll", 0);
      setModelMaterialModeValue("model", markTouched);
      break;
    case "npc":
      setNumber("modelTargetLongestCm", 180);
      setNumber("modelTriangleBudget", 12000);
      setNumber("modelFitScale", 100);
      setNumber("modelFitOffsetX", 0);
      setNumber("modelFitOffsetY", 0);
      setNumber("modelFitOffsetZ", 0);
      setNumber("modelFitPitch", 0);
      setNumber("modelFitYaw", 0);
      setNumber("modelFitRoll", 0);
      setModelMaterialModeValue("custom", markTouched);
      setNumber("modelPaintStrength", 100);
      setNumber("modelPaintMetallic", 0);
      setNumber("modelPaintRoughness", 62);
      break;
    case "container":
      setNumber("modelTargetLongestCm", 120);
      setNumber("modelTriangleBudget", 5000);
      setNumber("modelFitScale", 100);
      setNumber("modelFitOffsetX", 0);
      setNumber("modelFitOffsetY", 0);
      setNumber("modelFitOffsetZ", 0);
      setNumber("modelFitPitch", 0);
      setNumber("modelFitYaw", 0);
      setNumber("modelFitRoll", 0);
      setModelMaterialModeValue("model", markTouched);
      break;
    case "vehicle":
      setNumber("modelTargetLongestCm", 420);
      setNumber("modelTriangleBudget", 8000);
      setModelMaterialModeValue("game", markTouched);
      break;
    default:
      setNumber("modelTargetLongestCm", inferModelTargetLongestCm(field));
      setNumber("modelTriangleBudget", inferRawModelTriangleBudget(field, selectedRawModelImport()));
      setModelMaterialModeValue("model", markTouched);
      break;
  }

  syncModelTargetLongestSlider("number");
  syncWeaponAdapterControls(field);
  syncVehicleAdapterControls(field);
  syncModelCookProfileControls(field);
}

function syncModelCookProfileControls(field = null) {
  const rawModel = selectedRawModelImport();
  const inferredProfile = inferModelCookProfile(field);
  const activeProfile = state.modding.modelProfilePreset === "auto"
    ? inferredProfile
    : state.modding.modelProfilePreset;

  document.querySelectorAll(".model-profile-btn").forEach((button) => {
    const profile = button.dataset.modelProfile || "";
    const isActive = profile === state.modding.modelProfilePreset
      || (profile !== "auto" && state.modding.modelProfilePreset === "auto" && profile === inferredProfile);
    button.classList.toggle("active", isActive);
    button.setAttribute("aria-pressed", isActive ? "true" : "false");
  });

  const budgetInput = el("modelTriangleBudget");
  if (budgetInput && budgetInput.dataset.userTouched !== "1") {
    budgetInput.value = String(inferRawModelTriangleBudget(field, rawModel));
  }

  const summary = el("modelCookSummary");
  if (!summary) {
    return;
  }

  const budget = getModelFitNumber("modelTriangleBudget", inferRawModelTriangleBudget(field, rawModel));
  const materialMode = getModelMaterialMode();
  const profileName = {
    "two-hand-melee": "двуручный меч",
    weapon: "оружие",
    helmet: "шлем",
    armor: "броня / одежда",
    npc: "NPC / зомби",
    container: "контейнер",
    vehicle: "транспорт",
    generic: "универсальный"
  }[activeProfile] || "универсальный";
  const materialText = materialMode === "custom"
    ? "ручная окраска"
    : materialMode === "game"
      ? "материал из игры"
      : "материалы модели";
  const budgetText = budget > 0 ? `лимит ${budget.toLocaleString("ru-RU")} треугольников` : "оптимизация по необходимости";
  summary.textContent = `Профиль: ${profileName}. Размер: ${getModelFitNumber("modelTargetLongestCm", inferModelTargetLongestCm(field))} см. Материал: ${materialText}. Геометрия: ${budgetText}.`;
  renderModelGuidancePanel(field, activeProfile, rawModel, cookedCount);
  renderModelWorkflowSteps(field);
}

function renderModelGuidancePanel(field = null, profile = "generic", rawModel = null, cookedCount = 0) {
  const host = el("modelGuidancePanel");
  if (!host) {
    return;
  }

  host.innerHTML = "";
  const title = document.createElement("div");
  title.className = "model-guidance-title";
  const body = document.createElement("div");
  body.className = "model-guidance-body";

  const profileText = {
    "two-hand-melee": {
      title: "Двуручный меч",
      body: "Программа ищет реальную рукоять, ставит Grip на неё, сохраняет двухручные HandsCorrections катаны и двигает всю модель к поддерживающей руке без растяжения текстур."
    },
    weapon: {
      title: "Оружие",
      body: "Сохраняются игровые поля хвата, сокетов, анимаций и крепления. Raw-модель подгоняется вокруг точки удержания, чтобы игра продолжала использовать родной weapon contract."
    },
    helmet: {
      title: "Шлем",
      body: "Подгонка идёт под UpperHeadSocket и safe worn mesh fields. Внутренний HeadWear component не трогается, чтобы предмет можно было поднимать и надевать."
    },
    armor: {
      title: "Броня и одежда",
      body: "Комплект можно разложить по слотам: шлем, торс, руки, ноги, ботинки. Программа подбирает части raw-модели и готовит их с запасом поверх тела."
    },
    npc: {
      title: "NPC / зомби",
      body: "Raw-модель готовится как SkeletalMesh под skeleton/physics цели. Для интернет-моделей включается упрощение и безопасная окраска, чтобы cook не терял материалы и не ломал анимации."
    },
    container: {
      title: "Контейнер или предмет мира",
      body: "Pivot и размер подгоняются под grounded/world placement, чтобы модель стояла на земле и сохраняла игровые коллизии/интеракции цели."
    },
    vehicle: {
      title: "Транспорт",
      body: "Для транспорта используется отдельный contract: visual, query/collision, seats, entry points и sockets должны оставаться согласованными с оригиналом."
    },
    generic: {
      title: "Универсальная модель",
      body: "Сначала программа безопасно подставляет visual mesh и сохраняет игровые поля цели. Тонкую подгонку открывай только если модель заметно смещена в игре."
    }
  }[profile] || {
    title: "Универсальная модель",
    body: "Сначала программа безопасно подставляет visual mesh и сохраняет игровые поля цели."
  };

  title.textContent = profileText.title;
  const next = !field
    ? "Выбери игровую систему и visual-поле."
    : rawModel
      ? "Нажми «Подготовить в UE4», затем программа сама подставит cooked asset."
      : cookedCount > 0
        ? "Выбери cooked-модель и нажми «Подставить модель»."
        : "Загрузи FBX, OBJ, GLTF, DAE, BLEND или ZIP с моделью.";
  body.textContent = `${profileText.body} Следующий шаг: ${next}`;

  host.append(title, body);
}

function renderModelWorkflowSteps(field = null) {
  const host = el("modelWorkflowSteps");
  if (!host) {
    return;
  }

  host.innerHTML = "";
  const rawModel = selectedRawModelImport();
  const cookedCount = getCompatibleModelOptionsForField(field).length;
  const stagedCount = state.modding.stagedByAssetId.size;
  const steps = [
    {
      title: field ? "Цель выбрана" : "Выбери цель",
      note: field ? field.label || field.fieldPath || "visual field" : "Слот предмета или персонажа",
      ready: Boolean(field),
      active: !field
    },
    {
      title: rawModel ? "Raw-модель загружена" : cookedCount ? "Cooked-модель готова" : "Загрузи модель",
      note: rawModel?.name || (cookedCount ? `${cookedCount} cooked assets` : "FBX / OBJ / GLTF / DAE / ZIP"),
      ready: Boolean(rawModel || cookedCount),
      active: Boolean(field) && !rawModel && !cookedCount
    },
    {
      title: stagedCount ? "Правки в моде" : "Подготовь и собери",
      note: stagedCount ? `${stagedCount} ассетов в очереди` : "Cook / staged edits / PAK",
      ready: stagedCount > 0,
      active: Boolean(field) && Boolean(rawModel || cookedCount) && stagedCount === 0
    }
  ];

  for (const step of steps) {
    const node = document.createElement("div");
    node.className = "model-workflow-step";
    node.classList.toggle("is-ready", step.ready);
    node.classList.toggle("is-active", step.active);

    const title = document.createElement("div");
    title.className = "model-workflow-step-title";
    title.textContent = step.title;
    node.appendChild(title);

    const note = document.createElement("div");
    note.className = "model-workflow-step-note";
    note.textContent = step.note;
    note.title = step.note;
    node.appendChild(note);

    host.appendChild(node);
  }

  renderStudioFlowBar();
}

function renderStudioFlowBar() {
  const host = el("studioFlowBar");
  if (!host) {
    return;
  }

  const selectedAsset = state.modding.selectedAsset || selectedAssetFromCurrentPage();
  const rawCount = Array.isArray(state.modding.rawModelImports) ? state.modding.rawModelImports.length : 0;
  const cookedCount = Array.isArray(state.modding.customVisualModels) ? state.modding.customVisualModels.length : 0;
  const stagedCount = state.modding.stagedByAssetId.size;
  const pakReady = state.status?.unrealPakFound !== false;
  const modelReady = rawCount + cookedCount > 0;
  const steps = [
    {
      label: "Система",
      value: selectedAsset?.displayName || "не выбрана",
      ready: Boolean(selectedAsset),
      active: !selectedAsset
    },
    {
      label: "Модель",
      value: modelReady
        ? `${rawCount} raw / ${cookedCount} cooked`
        : "ожидает файл",
      ready: modelReady,
      active: Boolean(selectedAsset) && !modelReady
    },
    {
      label: "Изменения",
      value: stagedCount > 0 ? `${stagedCount} в моде` : "пусто",
      ready: stagedCount > 0,
      active: Boolean(selectedAsset) && modelReady && stagedCount === 0
    },
    {
      label: "PAK",
      value: stagedCount > 0 && pakReady ? "готов к сборке" : pakReady ? "после изменений" : "нет UnrealPak",
      ready: stagedCount > 0 && pakReady,
      active: stagedCount > 0 && pakReady
    }
  ];

  host.innerHTML = "";
  for (const step of steps) {
    const item = document.createElement("div");
    item.className = "studio-flow-step";
    item.classList.toggle("is-ready", step.ready);
    item.classList.toggle("is-active", step.active);

    const label = document.createElement("div");
    label.className = "studio-flow-label";
    label.textContent = step.label;

    const value = document.createElement("div");
    value.className = "studio-flow-value";
    value.textContent = step.value;
    value.title = step.value;

    item.append(label, value);
    host.appendChild(item);
  }
}

function inferModelTargetLongestCm(field = null) {
  const haystack = [
    state.modding.selectedAssetId,
    state.modding.selectedAsset?.relativePath,
    state.modding.currentSchema?.relativePath,
    field?.label,
    field?.section,
    field?.currentValue
  ].filter(Boolean).join(" ").toLowerCase();

  if (haystack.includes("airplane")
    || haystack.includes("/planes/")
    || haystack.includes("/plane_")
    || haystack.includes("aircraft")
    || haystack.includes("duster")
    || haystack.includes("kinglet")) {
    return 1800;
  }
  if (haystack.includes("vehicle") || haystack.includes("/vehicles/") || haystack.includes("transport")) {
    return 420;
  }
  if (haystack.includes("basebuilding")
    || haystack.includes("fortification")
    || haystack.includes("building")
    || haystack.includes("structure")
    || haystack.includes("wall")
    || haystack.includes("floor")
    || haystack.includes("door")
    || haystack.includes("gate")) {
    return 300;
  }
  if (haystack.includes("chest")
    || haystack.includes("crate")
    || haystack.includes("container")
    || haystack.includes("storage")
    || haystack.includes("wardrobe")
    || haystack.includes("locker")
    || haystack.includes("сундук")
    || haystack.includes("ящик")
    || haystack.includes("контейнер")) {
    return 120;
  }
  if (haystack.includes("weapon")
    || haystack.includes("/weapons/")
    || haystack.includes("new_melee")
    || haystack.includes("melee")
    || haystack.includes("katana")
    || haystack.includes("machete")
    || haystack.includes("knife")
    || haystack.includes("sword")
    || haystack.includes("ranged_weapons")
    || haystack.includes("shotgun")
    || haystack.includes("rifle")
    || haystack.includes("pistol")
    || haystack.includes("оруж")) {
    if (haystack.includes("katana")
      || haystack.includes("sword")
      || haystack.includes("2h_")
      || haystack.includes("twohand")) {
      return 115;
    }
    if (haystack.includes("machete")
      || haystack.includes("knife")
      || haystack.includes("1h_")) {
      return 80;
    }
    if (haystack.includes("sawed")
      || haystack.includes("short")
      || haystack.includes("обрез")) {
      return 65;
    }
    return 95;
  }
  if (haystack.includes("clothing")
    || haystack.includes("clothes")
    || haystack.includes("armor")
    || haystack.includes("vests_armor")
    || haystack.includes("torso_protection")
    || haystack.includes("underwear_pants")
    || haystack.includes("jackets_coats")
    || haystack.includes("gloves")
    || haystack.includes("footwear")
    || haystack.includes("backpack")
    || haystack.includes("рюкзак")) {
    return isArmorModelReplacementField(field) ? 85 : 75;
  }
  if (haystack.includes("npc")
    || haystack.includes("zombie")
    || haystack.includes("puppet")
    || haystack.includes("/characters/zombies")
    || haystack.includes("/characters/npcs")
    || haystack.includes("персонаж")
    || haystack.includes("зомби")) {
    return 180;
  }
  return getModelKindForReplacementField(field) === "skeletal-mesh" ? 100 : 150;
}

function inferRawModelTriangleBudget(field = null, rawModel = null) {
  const parts = Array.isArray(rawModel?.parts) ? rawModel.parts : [];
  const knownTriangles = parts.reduce((sum, part) => sum + Number(part.triangles || 0), 0);
  const haystack = [
    state.modding.selectedAssetId,
    state.modding.selectedAsset?.relativePath,
    state.modding.currentSchema?.relativePath,
    field?.label,
    field?.section,
    field?.currentValue
  ].filter(Boolean).join(" ").replace(/\\/g, "/").toLowerCase();

  if (getModelKindForReplacementField(field) === "skeletal-mesh") {
    if (isArmorModelReplacementField(field)) {
      const budget = 10000;
      return knownTriangles > budget || knownTriangles === 0 ? budget : 0;
    }
    if (isNpcCharacterModelReplacementField(field)) {
      const budget = 12000;
      return knownTriangles > budget || knownTriangles === 0 ? budget : 0;
    }
    return 0;
  }

  let budget = 6000;
  if (haystack.includes("airplane")
    || haystack.includes("vehicle")
    || haystack.includes("/vehicles/")
    || haystack.includes("duster")
    || haystack.includes("kinglet")) {
    budget = 8000;
  } else if (haystack.includes("building")
    || haystack.includes("structure")
    || haystack.includes("basebuilding")
    || haystack.includes("wall")
    || haystack.includes("gate")) {
    budget = 14000;
  } else if (haystack.includes("chest")
    || haystack.includes("crate")
    || haystack.includes("container")
    || haystack.includes("storage")) {
    budget = 5000;
  } else if (haystack.includes("item")
    || haystack.includes("gameresources")
    || haystack.includes("loot")) {
    budget = 3500;
  }

  return knownTriangles > budget ? budget : 0;
}

function setModelFitNumber(id, value) {
  const input = el(id);
  if (!input) {
    return;
  }

  input.value = String(Number.isFinite(Number(value)) ? value : 0);
}

function resetModelCookTouchedControls() {
  [
    "modelTargetLongestCm",
    "modelTriangleBudget",
    "modelFitScale",
    "modelFitOffsetX",
    "modelFitOffsetY",
    "modelFitOffsetZ",
    "modelFitPitch",
    "modelFitYaw",
    "modelFitRoll",
    "weaponGripAnchorPercent",
    "weaponGripDiameterCm",
    "weaponGripBackReachCm",
    "weaponSecondHandShiftCm",
    "modelPaintStrength",
    "modelPaintMetallic",
    "modelPaintRoughness",
    "modelMaterialMode"
  ].forEach((id) => {
    const input = el(id);
    if (input) {
      delete input.dataset.userTouched;
    }
  });
  state.modding.modelTargetLongestTouched = false;
}

function getModelMaterialMode() {
  return el("modelMaterialMode")?.value || "model";
}

function syncModelMaterialControls() {
  const mode = getModelMaterialMode();
  const referenceControls = el("modelMaterialReferenceControls");
  const referenceSearch = el("modelMaterialSearch");
  const referenceSelect = el("modelMaterialReference");
  const paintColor = el("modelPaintColor");
  const paintStrength = el("modelPaintStrength");
  const paintMetallic = el("modelPaintMetallic");
  const paintRoughness = el("modelPaintRoughness");
  const useReference = mode === "game";
  const useCustomPaint = mode === "custom";

  if (referenceControls) {
    referenceControls.hidden = !useReference;
  }
  if (referenceSearch) {
    referenceSearch.disabled = !useReference;
    referenceSearch.placeholder = isVehicleModelReplacementField(getSelectedModelReplacementField())
      ? "Например: MI_Plane_01_Body_A, cockpit, glass"
      : "Например: MI_M1887, wood, metal";
  }
  if (referenceSelect) {
    referenceSelect.disabled = !useReference;
  }

  for (const input of [paintColor, paintStrength, paintMetallic, paintRoughness]) {
    if (input) {
      input.disabled = !useCustomPaint;
    }
  }
}

function queueModelMaterialReferenceRefresh() {
  window.clearTimeout(modelMaterialSearchDebounce);
  modelMaterialSearchDebounce = window.setTimeout(() => {
    refreshModelMaterialReferenceOptions().catch(showError);
  }, 280);
}

async function refreshModelMaterialReferenceOptions() {
  const select = el("modelMaterialReference");
  const search = el("modelMaterialSearch");
  if (!select || !search || getModelMaterialMode() !== "game") {
    return;
  }

  const term = search.value.trim();
  select.innerHTML = "";
  if (term.length < 2) {
    const option = document.createElement("option");
    option.value = "";
    option.textContent = "Введи минимум 2 символа для поиска .mi/material";
    select.appendChild(option);
    return;
  }

  const token = ++modelMaterialOptionsToken;
  const loading = document.createElement("option");
  loading.value = "";
  loading.textContent = "Ищу материалы в игре и импортированных ассетах...";
  select.appendChild(loading);

  const options = await api(`/api/modding/reference-options?pickerKind=visual-material-object&term=${encodeURIComponent(term)}&limit=40`);
  if (token !== modelMaterialOptionsToken || getModelMaterialMode() !== "game") {
    return;
  }

  select.innerHTML = "";
  const items = Array.isArray(options) ? options : [];
  if (!items.length) {
    const option = document.createElement("option");
    option.value = "";
    option.textContent = "Материалы не найдены";
    select.appendChild(option);
    return;
  }

  for (const item of items) {
    const option = document.createElement("option");
    option.value = item.value || "";
    option.textContent = item.label || item.value || "material";
    select.appendChild(option);
  }
}

function getModelFitFields() {
  const schema = state.modding.currentSchema;
  const fields = Array.isArray(schema?.fields) ? schema.fields : [];
  const findField = (...tokens) => fields.find((field) => {
    const label = String(field.label || "").toLowerCase();
    return field.editable !== false && tokens.every((token) => label.includes(token));
  }) || null;

  return {
    offsetX: findField("смещение крепления", "позиция x"),
    offsetY: findField("смещение крепления", "позиция y"),
    offsetZ: findField("смещение крепления", "позиция z"),
    pitch: findField("смещение крепления", "pitch"),
    yaw: findField("смещение крепления", "yaw"),
    roll: findField("смещение крепления", "roll"),
    scale: findField("масштаб") || findField("scale")
  };
}

function seedModelFitControlsFromSchema() {
  const fitFields = getModelFitFields();
  const pairs = [
    ["modelFitOffsetX", fitFields.offsetX],
    ["modelFitOffsetY", fitFields.offsetY],
    ["modelFitOffsetZ", fitFields.offsetZ],
    ["modelFitPitch", fitFields.pitch],
    ["modelFitYaw", fitFields.yaw],
    ["modelFitRoll", fitFields.roll]
  ];

  for (const [id, field] of pairs) {
    if (!field) {
      continue;
    }

    const currentValue = state.modding.currentFieldValues.get(field.fieldPath) ?? field.currentValue ?? "0";
    setModelFitNumber(id, currentValue);
  }

  const targetInput = el("modelTargetLongestCm");
  const replacementField = getSelectedModelReplacementField();
  if (targetInput && (!state.modding.modelTargetLongestTouched || !targetInput.value)) {
    targetInput.value = String(inferModelTargetLongestCm(replacementField));
  }
  syncModelTargetLongestSlider("number");
  syncVehicleAdapterControls(replacementField);
  syncWeaponAdapterControls(replacementField);
}

function applyModelFitField(field, value, changedLabels) {
  if (!field) {
    return;
  }

  const formatted = String(Number(value).toFixed(3)).replace(/\.?0+$/, "");
  setCurrentFieldValue(field.fieldPath, formatted, {
    displayValue: formatted,
    renderScene: false
  });
  changedLabels.push(field.label);
}

function setModelReplacementStatus(text, isError = false) {
  const host = el("modelReplacementStatus");
  if (!host) {
    return;
  }

  host.textContent = text || "";
  host.classList.toggle("status-error", Boolean(isError));
}

function populateModelReplacementAssets(field) {
  const modelSelect = el("modelReplacementAsset");
  if (!modelSelect) {
    return [];
  }

  const models = getCompatibleModelOptionsForField(field);
  modelSelect.innerHTML = "";
  for (const model of models) {
    const option = document.createElement("option");
    option.value = model.targetRelativePath;
    option.textContent = `${formatCustomVisualKind(model.kind)}: ${model.name}`;
    modelSelect.appendChild(option);
  }

  return models;
}

function getSelectedModelReplacementField() {
  const fieldSelect = el("modelReplacementField");
  const fields = getModelReplacementFields();
  return fields.find((field) => field.fieldPath === fieldSelect?.value) || fields[0] || null;
}

function getModelKindForReplacementField(field) {
  const pickerKind = String(field?.referencePickerKind || "").toLowerCase();
  return pickerKind.includes("skeletal") ? "skeletal-mesh" : "static-mesh";
}

function populateRawModelCookControls(field) {
  const host = el("rawModelCookControls");
  const select = el("rawModelCookSource");
  const button = el("rawModelCookBtn");
  const vehicleFullButton = el("vehicleFullReplacementBtn");
  if (!host || !select || !button) {
    return;
  }

  const rawModels = state.modding.rawModelImports || [];
  host.hidden = rawModels.length === 0 || !field;
  const previousValue = select.value;
  select.innerHTML = "";
  for (const model of rawModels) {
    const option = document.createElement("option");
    option.value = model.sourceRelativePath || "";
    const bounds = formatModelBounds(model.bounds);
    option.textContent = bounds
      ? `${model.format || "MODEL"}: ${model.name} (${bounds})`
      : `${model.format || "MODEL"}: ${model.name}`;
    select.appendChild(option);
  }
  if (rawModels.some((model) => model.sourceRelativePath === previousValue)) {
    select.value = previousValue;
  }

  const vehicleIssue = getVehicleModelReplacementIssue(field, { rawCook: true });
  button.disabled = rawModels.length === 0 || !field || Boolean(vehicleIssue);
  button.title = vehicleIssue || "Программа сама запустит Blender/UE4.27 в фоне и добавит cooked asset в список моделей.";
  if (vehicleFullButton) {
    const showVehicleFull = VEHICLE_ADAPTER_CLIENT_VISIBLE && rawModels.length > 0 && field && isVehicleAssetContext();
    vehicleFullButton.hidden = !showVehicleFull;
    vehicleFullButton.disabled = !showVehicleFull || state.modding.vehicleFullReplacementCooking;
    vehicleFullButton.textContent = state.modding.vehicleFullReplacementCooking
      ? "Собираю полную замену..."
      : "Полная замена транспорта";
    vehicleFullButton.title = "Оставляет оригинальную физику транспорта, готовит новую модель как visual overlay, глушит старые визуальные attachment-модули и сразу собирает/ставит pak.";
  }
  syncModelCookProfileControls(field);
  renderRawModelAnalysisPanel();
}

async function cookVehicleFullReplacementForCurrentSelection() {
  if (!VEHICLE_ADAPTER_CLIENT_VISIBLE) {
    setModelReplacementStatus("Замена моделей техники временно скрыта: vehicle adapter оставлен только для внутренней доработки.", true);
    return;
  }

  const select = el("rawModelCookSource");
  const rawModel = (state.modding.rawModelImports || [])
    .find((model) => model.sourceRelativePath === select?.value);
  if (!state.modding.selectedAssetId || !rawModel?.sourceRelativePath || !isVehicleAssetContext()) {
    setModelReplacementStatus("Выбери транспортный ассет SCUM и загруженную raw-модель.", true);
    return;
  }

  state.modding.vehicleFullReplacementCooking = true;
  populateRawModelCookControls(getSelectedModelReplacementField());
  setModelReplacementStatus("Готовлю полную замену транспорта: visual overlay, оригинальный physics contract, tiny suppressors, сборка pak и установка в SCUM. Это может занять несколько минут...");

  try {
    const materialMode = getModelMaterialMode();
    const materialReference = materialMode === "game"
      ? (el("modelMaterialReference")?.value || "")
      : "";
    const payload = {
      assetId: state.modding.selectedAssetId,
      rawSourceRelativePath: rawModel.sourceRelativePath,
      installToGame: true,
      modName: "valkyrie_duster_full_replacement",
      targetLongestCm: getModelFitNumber("modelTargetLongestCm", 1800),
      targetTriangleCount: getModelFitNumber("modelTriangleBudget", inferRawModelTriangleBudget(getSelectedModelReplacementField(), rawModel)) || 45000,
      materialMode,
      materialReference,
      scalePercent: getModelFitNumber("modelFitScale", 100),
      offsetX: getModelFitNumber("modelFitOffsetX", -500),
      offsetY: getModelFitNumber("modelFitOffsetY", 0),
      offsetZ: getModelFitNumber("modelFitOffsetZ", 70),
      pitch: getModelFitNumber("modelFitPitch", 0),
      yaw: getModelFitNumber("modelFitYaw", 0),
      roll: getModelFitNumber("modelFitRoll", 0),
      paintColorHex: el("modelPaintColor")?.value || "#ffffff",
      paintStrengthPercent: materialMode === "custom" ? getModelFitNumber("modelPaintStrength", 0) : 0,
      metallicPercent: getModelFitNumber("modelPaintMetallic", 0),
      roughnessPercent: getModelFitNumber("modelPaintRoughness", 50),
      collisionMode: el("vehicleCollisionMode")?.value || "visual-query",
      queryProxyLengthPercent: getModelFitNumber("vehicleQueryProxyLength", 96),
      queryProxyWidthPercent: getModelFitNumber("vehicleQueryProxyWidth", 88),
      queryProxyHeightPercent: getModelFitNumber("vehicleQueryProxyHeight", 92),
      seatOffsetX: getModelFitNumber("vehicleSeatOffsetX", 0),
      seatOffsetY: getModelFitNumber("vehicleSeatOffsetY", -12),
      seatOffsetZ: getModelFitNumber("vehicleSeatOffsetZ", 0),
      passengerSeatOffsetX: getModelFitNumber("vehiclePassengerSeatOffsetX", 0),
      passengerSeatOffsetY: getModelFitNumber("vehiclePassengerSeatOffsetY", -12),
      passengerSeatOffsetZ: getModelFitNumber("vehiclePassengerSeatOffsetZ", -5)
    };
    [
      ["vehicleEntryOffsetX", "entryOffsetX"],
      ["vehicleEntryOffsetY", "entryOffsetY"],
      ["vehicleEntryOffsetZ", "entryOffsetZ"]
    ].forEach(([inputId, payloadKey]) => {
      const input = el(inputId);
      if (input?.dataset.userTouched === "1") {
        payload[payloadKey] = getModelFitNumber(inputId, 0);
      }
    });

    const result = await api("/api/modding/vehicle-full-replacement", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    const warnings = Array.isArray(result.warnings) ? result.warnings : [];
    if (result.ok === false) {
      setModelReplacementStatus(`${result.error || "Полную замену транспорта не удалось собрать."}${warnings.length ? ` ${warnings.slice(0, 5).join(" ")}` : ""}`, true);
      return;
    }

    await fetchCustomVisualModels();
    let stagedCount = 0;
    for (const edit of result.suggestedEdits || []) {
      if (stageSuggestedAssetEditFromCook(edit, {
        targetDisplayName: state.modding.selectedAsset?.displayName || "vehicle full replacement",
        targetRelativePath: state.modding.selectedAsset?.relativePath || state.modding.selectedAssetId
      })) {
        stagedCount += 1;
      }
    }

    refreshModelReplacementWizard();
    const pakPath = result.buildResult?.installedPakPath || result.buildResult?.outputPakPath || "";
    const suppressors = Array.isArray(result.suppressorCookResults)
      ? result.suppressorCookResults.filter((item) => item.ok).length
      : 0;
    setModelReplacementStatus(`Полная замена транспорта собрана и установлена. Pak: ${pakPath || "готов"}. Staged edits: ${stagedCount}, suppressors: ${suppressors}. ${warnings.slice(0, 4).join(" ")}`);
  } finally {
    state.modding.vehicleFullReplacementCooking = false;
    populateRawModelCookControls(getSelectedModelReplacementField());
  }
}

async function cookSelectedRawModelForReplacement() {
  const select = el("rawModelCookSource");
  const button = el("rawModelCookBtn");
  const field = getSelectedModelReplacementField();
  const rawModel = (state.modding.rawModelImports || [])
    .find((model) => model.sourceRelativePath === select?.value);

  if (!field || !rawModel) {
    setModelReplacementStatus("Выбери игровое поле модели и загруженную raw-модель.", true);
    return;
  }

  const vehicleIssue = getVehicleModelReplacementIssue(field, { rawCook: true });
  if (vehicleIssue) {
    setModelReplacementStatus(vehicleIssue, true);
    return;
  }

  const materialMode = getModelMaterialMode();
  const materialReference = el("modelMaterialReference")?.value || "";
  if (materialMode === "game" && !materialReference) {
    setModelReplacementStatus("Для режима «Из игры / импортированный .mi» найди и выбери материал, который нужно привязать к модели.", true);
    return;
  }

  setModelReplacementStatus("Готовлю модель: Blender/UE4.27 будут запущены в фоне. Это может занять несколько минут...");
  if (button) {
    button.disabled = true;
  }

  try {
    const result = await api("/api/custom-visual-assets/cook-raw", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        rawSourceRelativePath: rawModel.sourceRelativePath,
        assetId: state.modding.selectedAssetId,
        fieldPath: field.fieldPath,
        modelKind: getModelKindForReplacementField(field),
        scalePercent: getModelFitNumber("modelFitScale", 100),
        offsetX: getModelFitNumber("modelFitOffsetX"),
        offsetY: getModelFitNumber("modelFitOffsetY"),
        offsetZ: getModelFitNumber("modelFitOffsetZ"),
        pitch: getModelFitNumber("modelFitPitch"),
        yaw: getModelFitNumber("modelFitYaw"),
        roll: getModelFitNumber("modelFitRoll"),
        autoFitToTarget: el("modelAutoFit")?.checked !== false,
        targetLongestCm: getModelFitNumber("modelTargetLongestCm", inferModelTargetLongestCm(field)),
        paintColorHex: el("modelPaintColor")?.value || "#ffffff",
        paintStrengthPercent: materialMode === "custom" ? getModelFitNumber("modelPaintStrength", 100) : 0,
        metallicPercent: getModelFitNumber("modelPaintMetallic"),
        roughnessPercent: getModelFitNumber("modelPaintRoughness", 50),
        materialMode,
        materialReference,
        targetTriangleCount: getModelFitNumber("modelTriangleBudget", inferRawModelTriangleBudget(field, rawModel)),
        weaponGripAnchorPercent: getModelFitNumber("weaponGripAnchorPercent", 45),
        weaponGripDiameterCm: getModelFitNumber("weaponGripDiameterCm", 0),
        weaponGripBackReachCm: getModelFitNumber("weaponGripBackReachCm", 32),
        weaponSecondHandShiftCm: getModelFitNumber("weaponSecondHandShiftCm", 24)
      })
    });

    const warnings = Array.isArray(result.warnings) ? result.warnings : [];
    if (result.ok === false) {
      const tail = result.unrealLogTail || result.blenderLogTail || "";
      const tailPreview = tail ? `\n${tail.split("\n").slice(-4).join("\n")}` : "";
      setModelReplacementStatus(`${result.error || "Не удалось приготовить модель."}${warnings.length ? `\n${warnings.join(" ")}` : ""}${tailPreview}`, true);
      return;
    }

    const cookedAssets = Array.isArray(result.assets) ? result.assets : [];
    await fetchCustomVisualModels();
    refreshModelReplacementWizard();

    const cooked = cookedAssets[0];
    const modelSelect = el("modelReplacementAsset");
    if (cooked?.targetRelativePath && modelSelect) {
      modelSelect.value = cooked.targetRelativePath;
      applyModelReplacement();
      let stageText = "";
      try {
        const stagedItem = stageCurrentAssetEdits();
        stageText = stagedItem
          ? " Изменение уже добавлено в мод."
          : " Модель подставлена, но staged-изменений не найдено.";
      } catch (error) {
        stageText = " Модель подставлена; если стадия не появилась, нажми «Сохранить изменения в мод».";
      }
      const warningText = warnings.length ? ` Предупреждения: ${warnings.join(" ")}` : "";
      const paintText = materialMode === "custom"
        ? " Создан UE4-материал с выбранной окраской."
        : materialMode === "game"
          ? " Привязан выбранный .mi/material."
        : "";
      const fitText = el("modelAutoFit")?.checked !== false
        ? ` Размер подогнан под ${getModelFitNumber("modelTargetLongestCm", inferModelTargetLongestCm(field))} см.`
        : "";
      setModelReplacementStatus(`Raw-модель приготовлена и подставлена в выбранный ассет.${stageText}${fitText}${paintText}${warningText}`);
    } else {
      setModelReplacementStatus("Raw-модель приготовлена. Выбери её в списке новых моделей и нажми «Подставить модель».");
    }
  } finally {
    if (button) {
      button.disabled = false;
    }
  }
}

function refreshModelReplacementWizard() {
  const host = el("modelReplacementWizard");
  const fieldSelect = el("modelReplacementField");
  const applyBtn = el("modelReplacementApplyBtn");
  if (!host || !fieldSelect || !applyBtn) {
    return;
  }

  const fields = getModelReplacementFields();
  host.hidden = fields.length === 0;
  if (!fields.length) {
    setModelReplacementStatus("");
    renderRawModelAnalysisPanel();
    syncVehicleAdapterControls(null);
    syncWeaponAdapterControls(null);
    return;
  }

  const previousField = fieldSelect.value;
  fieldSelect.innerHTML = "";
  for (const field of fields) {
    const option = document.createElement("option");
    option.value = field.fieldPath;
    option.textContent = `${field.section || "Внешний вид"}: ${field.label}`;
    fieldSelect.appendChild(option);
  }

  if (fields.some((field) => field.fieldPath === previousField)) {
    fieldSelect.value = previousField;
  }

  const selectedField = fields.find((field) => field.fieldPath === fieldSelect.value) || fields[0];
  const materialModeSelect = el("modelMaterialMode");
  syncVehicleAdapterControls(selectedField);
  syncWeaponAdapterControls(selectedField);

  const models = populateModelReplacementAssets(selectedField);
  populateRawModelCookControls(selectedField);
  syncModelMaterialControls();
  const fitFields = getModelFitFields();
  const hasRuntimeFit = Boolean(fitFields.offsetX || fitFields.offsetY || fitFields.offsetZ || fitFields.pitch || fitFields.yaw || fitFields.roll || fitFields.scale);
  const applyIssue = getVehicleModelReplacementIssue(selectedField);
  const rawCookIssue = getVehicleModelReplacementIssue(selectedField, { rawCook: true });
  applyBtn.disabled = models.length === 0 || Boolean(applyIssue);
  if (applyIssue || rawCookIssue) {
    setModelReplacementStatus(applyIssue || rawCookIssue, true);
  } else if (!models.length) {
    const rawCount = (state.modding.rawModelImports || []).length;
    setModelReplacementStatus(rawCount > 0
      ? "Сырые модели сохранены как заготовки. После конвертации в cooked UE4.27 модель появится в списке."
      : "Загрузи cooked StaticMesh/SkeletalMesh .uasset с companion .uexp, затем выбери его здесь.", false);
  } else {
    setModelReplacementStatus(hasRuntimeFit
      ? `Доступно пользовательских моделей: ${models.length}. Смещение и поворот можно применить сразу к выбранному ассету.`
      : `Доступно пользовательских моделей: ${models.length}. Масштаб будет использоваться на этапе подготовки/cook модели.`);
  }
}

async function refreshModelReplacementModels() {
  setModelReplacementStatus("Обновляю список пользовательских моделей...");
  await fetchCustomVisualModels();
  refreshModelReplacementWizard();
}

function applyModelReplacement() {
  const modelSelect = el("modelReplacementAsset");
  const field = getSelectedModelReplacementField();
  const model = (state.modding.customVisualModels || [])
    .find((x) => x.targetRelativePath === modelSelect?.value);

  if (!field || !model) {
    setModelReplacementStatus("Выбери поле предмета и cooked модель.", true);
    return;
  }

  const vehicleIssue = getVehicleModelReplacementIssue(field);
  if (vehicleIssue) {
    setModelReplacementStatus(vehicleIssue, true);
    return;
  }

  const value = getModelReferenceValueForField(model, field);
  if (!value) {
    setModelReplacementStatus("У выбранной модели нет подходящей ссылки для этого поля.", true);
    return;
  }

  const displayValue = `Пользовательский ассет: ${model.name}`;
  setCurrentFieldValue(field.fieldPath, value, {
    displayValue,
    renderScene: false
  });

  const fitFields = getModelFitFields();
  const changedFitLabels = [];
  applyModelFitField(fitFields.offsetX, getModelFitNumber("modelFitOffsetX"), changedFitLabels);
  applyModelFitField(fitFields.offsetY, getModelFitNumber("modelFitOffsetY"), changedFitLabels);
  applyModelFitField(fitFields.offsetZ, getModelFitNumber("modelFitOffsetZ"), changedFitLabels);
  applyModelFitField(fitFields.pitch, getModelFitNumber("modelFitPitch"), changedFitLabels);
  applyModelFitField(fitFields.yaw, getModelFitNumber("modelFitYaw"), changedFitLabels);
  applyModelFitField(fitFields.roll, getModelFitNumber("modelFitRoll"), changedFitLabels);

  const scalePercent = getModelFitNumber("modelFitScale", 100);
  if (fitFields.scale) {
    applyModelFitField(fitFields.scale, scalePercent / 100, changedFitLabels);
  }

  renderSchemaFields();
  refreshModelReplacementWizard();
  const fitNote = changedFitLabels.length
    ? `Также применены fit-поля: ${changedFitLabels.length}.`
    : `Масштаб ${scalePercent}% сохранён как параметр подготовки модели и будет применяться при raw→cooked конвертации.`;
  setModelReplacementStatus(`Модель подставлена. ${fitNote} Профиль рук, крепления и offsets остаются от выбранного предмета, чтобы новая модель безопасно села в игровую логику.`);
}

async function loadStatus() {
  const status = await api("/api/status");
  state.status = status;
  renderStatusLine(status.features?.length || 0);
}

function renderStatusLine(categoryCount = 0) {
  if (!state.status) {
    return;
  }

  const status = state.status;
  const scumText = status.scumFound
    ? status.scumRoot
    : "не найдена";
  const pakText = status.unrealPakFound
    ? "UnrealPak готов"
    : "UnrealPak не найден";
  const buildText = status.buildId || "неизвестно";
  el("statusLine").textContent =
    `SCUM: ${scumText} | Сборка игры: ${buildText} | ${pakText} | Разделов в студии: ${categoryCount}`;
  renderToolchainStatus(status.toolchain);
  renderStudioFlowBar();
}

function renderToolchainStatus(toolchain) {
  const host = el("toolchainStatus");
  if (!host) {
    return;
  }

  host.innerHTML = "";
  if (!toolchain || !Array.isArray(toolchain.steps)) {
    return;
  }

  const summary = document.createElement("span");
  summary.className = `toolchain-chip ${toolchain.readyForRawModelCook ? "toolchain-chip-ready" : "toolchain-chip-warn"}`;
  summary.textContent = toolchain.summary || "Проверка модкита";
  host.appendChild(summary);

  for (const step of toolchain.steps) {
    const chip = document.createElement("span");
    chip.className = `toolchain-chip ${step.ready ? "toolchain-chip-ready" : "toolchain-chip-warn"}`;
    chip.title = step.path ? `${step.description}\n${step.path}` : (step.description || "");
    chip.textContent = `${step.name}: ${step.status}`;
    host.appendChild(chip);
  }

  renderToolchainInstallHelp(toolchain.steps);
}

function renderToolchainInstallHelp(steps) {
  const host = el("toolchainInstallHelp");
  if (!host) {
    return;
  }

  host.innerHTML = "";
  const missingSteps = (Array.isArray(steps) ? steps : [])
    .filter((step) => !step.ready && Array.isArray(step.installSteps) && step.installSteps.length > 0);
  for (const step of missingSteps) {
    const details = document.createElement("details");
    details.className = "toolchain-help-card";

    const summary = document.createElement("summary");
    summary.textContent = step.installTitle || `Как включить: ${step.name}`;
    details.appendChild(summary);

    const list = document.createElement("ol");
    for (const installStep of step.installSteps) {
      const item = document.createElement("li");
      item.textContent = installStep;
      list.appendChild(item);
    }
    details.appendChild(list);

    const actions = document.createElement("div");
    actions.className = "toolchain-help-actions";
    if (step.actionLabel && step.actionUrl) {
      const action = document.createElement("button");
      action.type = "button";
      action.textContent = step.actionLabel;
      action.addEventListener("click", () => {
        window.open(step.actionUrl, "_blank", "noopener,noreferrer");
      });
      actions.appendChild(action);
    }

    if (step.actionLabel && !step.actionUrl) {
      const action = document.createElement("button");
      action.type = "button";
      action.textContent = step.actionLabel;
      action.addEventListener("click", () => {
        api("/api/toolchain/open-tools-folder", { method: "POST" }).catch(showError);
      });
      actions.appendChild(action);
    }

    if (step.id !== "pak-build") {
      const openTools = document.createElement("button");
      openTools.type = "button";
      openTools.textContent = "Открыть папку tools";
      openTools.addEventListener("click", () => {
        api("/api/toolchain/open-tools-folder", { method: "POST" }).catch(showError);
      });
      actions.appendChild(openTools);
    }

    if (actions.childElementCount) {
      details.appendChild(actions);
    }

    host.appendChild(details);
  }
}

function renderAppUpdateBanner() {
  const banner = el("updateBanner");
  const checkBtn = el("updateCheckBtn");
  const downloadBtn = el("updateDownloadBtn");
  const installBtn = el("updateInstallBtn");
  const titleEl = el("updateTitle");
  const summaryEl = el("updateSummary");
  const notesEl = el("updateNotes");
  const progressEl = el("updateProgress");
  const status = state.appUpdate.status;

  if (!banner || !checkBtn || !downloadBtn || !installBtn || !titleEl || !summaryEl || !notesEl || !progressEl) {
    return;
  }

  checkBtn.textContent = status?.isChecking ? "Проверяем..." : "Проверить обновления";
  checkBtn.disabled = !(status?.canCheck ?? false);

  if (!status) {
    banner.hidden = true;
    downloadBtn.hidden = true;
    installBtn.hidden = true;
    progressEl.textContent = "";
    notesEl.textContent = "";
    return;
  }

  const showBanner = Boolean(
    status.pendingRestart
    || status.updateAvailable
    || status.isChecking
    || status.isDownloading
    || status.isInstalling
    || status.lastError
  );

  banner.hidden = !showBanner;
  titleEl.textContent = status.statusTitle || "Обновление программы";

  const summaryParts = [];
  if (status.statusMessage) {
    summaryParts.push(status.statusMessage);
  }
  if (status.currentVersion) {
    summaryParts.push(`Текущая версия: ${status.currentVersion}.`);
  }
  if (status.availableVersion && !status.pendingRestart) {
    summaryParts.push(`Новая версия: ${status.availableVersion}.`);
  }
  if (status.pendingVersion) {
    summaryParts.push(`Готово к установке: ${status.pendingVersion}.`);
  }

  const checkedAt = formatUpdateTime(status.lastCheckedUtc);
  if (checkedAt && !status.isDownloading) {
    summaryParts.push(`Последняя проверка: ${checkedAt}.`);
  }

  summaryEl.textContent = summaryParts.join(" ").trim();

  const notes = releaseNotesToPlainText(status.releaseNotesMarkdown);
  notesEl.textContent = notes ? `Что нового: ${notes}` : "";

  if (status.isDownloading) {
    progressEl.textContent = `Скачано: ${status.downloadProgress || 0}%`;
  } else if (status.pendingRestart) {
    progressEl.textContent = "Новая версия уже скачана и ждёт установки.";
  } else {
    progressEl.textContent = "";
  }

  downloadBtn.hidden = !status.canDownload;
  downloadBtn.disabled = !status.canDownload;
  installBtn.hidden = !status.canInstall;
  installBtn.disabled = !status.canInstall;
}

async function loadAppUpdateStatus(silent = false) {
  try {
    state.appUpdate.status = await api("/api/app-update/status");
    renderAppUpdateBanner();
  } catch (err) {
    if (!silent) {
      showError(err);
    }
  }
}

async function runAppUpdateAction(path) {
  const result = await api(path, { method: "POST" });
  if (!result?.ok) {
    throw new Error(result?.error || "Не удалось выполнить действие обновления.");
  }

  await loadAppUpdateStatus(true);
  return result;
}

function renderCategorySelect() {
  const select = el("modCategorySelect");
  select.innerHTML = "";

  const all = document.createElement("option");
  all.value = "";
  all.textContent = "Все разделы";
  select.appendChild(all);

  for (const category of state.modding.categories) {
    const option = document.createElement("option");
    option.value = category.categoryId;
    option.textContent = `${category.name} (${category.assetCount})`;
    select.appendChild(option);
  }

  select.value = state.modding.selectedCategoryId;
  renderCategoryChips();
}

function renderCategoryChips() {
  const host = el("modCategoryChips");
  if (!host) {
    return;
  }

  host.innerHTML = "";

  const allButton = document.createElement("button");
  allButton.type = "button";
  allButton.className = "category-chip";
  allButton.dataset.categoryId = "";
  const allSelected = !state.modding.selectedCategoryId;
  allButton.classList.toggle("selected", allSelected);
  allButton.setAttribute("aria-pressed", allSelected ? "true" : "false");
  allButton.setAttribute("aria-label", "Показать все разделы");
  allButton.title = "Показать все безопасные разделы, доступные для моддинга.";
  allButton.textContent = "Все разделы";
  allButton.addEventListener("click", () => {
    if (!state.modding.selectedCategoryId) {
      return;
    }
    state.modding.selectedCategoryId = "";
    el("modCategorySelect").value = "";
    state.modding.page = 1;
    renderCategoryChips();
    loadModdingAssets().catch(showError);
  });
  host.appendChild(allButton);

  for (const category of state.modding.categories) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "category-chip";
    button.dataset.categoryId = category.categoryId;
    const selected = category.categoryId === state.modding.selectedCategoryId;
    button.classList.toggle("selected", selected);
    button.setAttribute("aria-pressed", selected ? "true" : "false");
    button.setAttribute("aria-label", `Категория: ${category.name}`);
    button.title = category.description || category.name;
    button.textContent = `${category.name} (${category.assetCount})`;
    button.addEventListener("click", () => {
      if (state.modding.selectedCategoryId === category.categoryId) {
        return;
      }
      state.modding.selectedCategoryId = category.categoryId;
      el("modCategorySelect").value = category.categoryId;
      state.modding.page = 1;
      renderCategoryChips();
      loadModdingAssets().catch(showError);
    });
    host.appendChild(button);
  }
}

async function loadModdingCategories() {
  const categories = await api("/api/modding/categories");
  state.modding.categories = Array.isArray(categories) ? categories : [];
  if (state.status) {
    renderStatusLine(state.modding.categories.length);
  }
  if (!state.modding.selectedCategoryId) {
    const preferredCategory = [
      "weapons-items",
      "npc-encounters",
      "vehicles",
      "crafting-recipes",
      "body-effects"
    ].find((categoryId) => state.modding.categories.some((x) => x.categoryId === categoryId));
    if (preferredCategory) {
      state.modding.selectedCategoryId = preferredCategory;
    }
  }
  renderCategorySelect();
}

function renderModPaging() {
  el("modPageInfo").textContent = `Страница ${state.modding.page} / ${modPageCount()}`;
  el("modPrevBtn").disabled = state.modding.page <= 1;
  el("modNextBtn").disabled = state.modding.page >= modPageCount();
}

function updateModAssetMeta() {
  const visibleAssets = getVisibleAssets();
  const pageAssets = state.modding.assets.length;
  const stagedAssets = state.modding.stagedByAssetId.size;
  const filteredNote =
    state.modding.showOnlyEditable && visibleAssets.length !== pageAssets
      ? ` Проверенных систем на этой странице: ${visibleAssets.length}.`
      : "";
  el("modAssetMeta").textContent =
    `Проверенных систем: ${state.modding.total}. На странице: ${pageAssets}.${filteredNote} Уже в моде: ${stagedAssets}.`;
}

function selectedAssetFromCurrentPage() {
  return state.modding.assets.find((x) => x.assetId === state.modding.selectedAssetId) || null;
}

function makeAssetFlag(text, extraClass = "") {
  const flag = document.createElement("span");
  flag.className = `asset-flag ${extraClass}`.trim();
  flag.textContent = text;
  return flag;
}

function renderSelectedAssetPreview() {
  const host = el("selectedAssetPreview");
  host.innerHTML = "";
  renderStudioFlowBar();

  const asset = state.modding.selectedAsset;
  if (!asset) {
    const empty = document.createElement("div");
    empty.className = "muted";
    empty.textContent = state.modding.showOnlyEditable
      ? "По текущему фильтру на этой странице нет систем с безопасными настройками."
      : "Система ещё не выбрана.";
    host.appendChild(empty);
    return;
  }

  const overline = document.createElement("div");
  overline.className = "selected-asset-overline";
  overline.textContent = asset.categoryName || "Игровая система";

  const title = document.createElement("div");
  title.className = "selected-asset-title";
  title.textContent = asset.displayName || asset.relativePath;

  const summary = document.createElement("div");
  summary.className = "selected-asset-summary";
  summary.textContent = asset.summary || "У этой системы есть понятные игровые параметры.";

  const flags = document.createElement("div");
  flags.className = "selected-asset-flags";
  flags.appendChild(makeAssetFlag(asset.supportsSafeEdits ? "Можно менять" : "Только просмотр", asset.supportsSafeEdits ? "asset-flag-good" : "asset-flag-warn"));
  if (state.modding.stagedByAssetId.has(asset.assetId)) {
    flags.appendChild(makeAssetFlag("Уже добавлено в мод", "asset-flag-good"));
  }

  const note = document.createElement("div");
  note.className = "selected-asset-note";
  note.textContent = asset.supportsSafeEdits
    ? "Проверенная редактируемая поверхность текущей сборки SCUM."
    : "Техническая поверхность без подтверждённых безопасных настроек.";

  const actions = document.createElement("div");
  actions.className = "selected-asset-actions";

  const openSchemaBtn = document.createElement("button");
  openSchemaBtn.type = "button";
  openSchemaBtn.textContent = "Перейти к настройкам";
  openSchemaBtn.addEventListener("click", () => {
    const scrollToSchema = () => {
      document.getElementById("schemaPanel")?.scrollIntoView({ behavior: "smooth", block: "start" });
    };

    if (state.modding.currentSchema?.assetId === asset.assetId) {
      scrollToSchema();
      return;
    }

    loadSelectedAssetSchema()
      .then(scrollToSchema)
      .catch(showError);
  });

  const reloadBtn = document.createElement("button");
  reloadBtn.type = "button";
  reloadBtn.textContent = "Обновить данные системы";
  reloadBtn.addEventListener("click", () => {
    loadSelectedAssetSchema().catch(showError);
  });

  actions.append(openSchemaBtn, reloadBtn);
  host.append(overline, title, summary, flags, note, actions);
}

function renderModAssetRows() {
  const host = el("modAssetRows");
  host.innerHTML = "";
  const visibleAssets = syncSelectedAssetWithVisibleList();

  if (!visibleAssets.length) {
    const empty = document.createElement("div");
    empty.className = "asset-list-empty muted";
    empty.textContent = state.modding.showOnlyEditable
      ? "На этой странице нет проверенных систем."
      : "По текущему фильтру игровые системы не найдены.";
    host.appendChild(empty);
    renderSelectedAssetPreview();
    updateModAssetMeta();
    renderStudioFlowBar();
    renderModPaging();
    return;
  }

  for (const asset of visibleAssets) {
    const card = document.createElement("button");
    card.type = "button";
    card.className = "asset-list-item";
    const selected = asset.assetId === state.modding.selectedAssetId;
    if (selected) {
      card.classList.add("selected");
    }
    card.setAttribute("aria-pressed", selected ? "true" : "false");

    const title = document.createElement("strong");
    title.className = "asset-list-item-title";
    title.textContent = asset.displayName || asset.relativePath;

    const badge = document.createElement("span");
    badge.className = "asset-list-item-arrow";
    badge.textContent = asset.assetId === state.modding.selectedAssetId ? "Выбрано" : "Открыть";

    const top = document.createElement("div");
    top.className = "asset-list-item-top";
    top.append(title, badge);

    const summary = document.createElement("div");
    summary.className = "asset-list-item-summary small muted";
    summary.textContent = asset.summary || "Понятные и безопасные настройки этой системы.";

    const flags = document.createElement("div");
    flags.className = "asset-list-item-flags";
    flags.appendChild(makeAssetFlag(asset.categoryName || "Система"));
    flags.appendChild(makeAssetFlag(
      asset.supportsSafeEdits ? "Можно менять" : "Только просмотр",
      asset.supportsSafeEdits ? "asset-flag-good" : "asset-flag-warn"));
    if (state.modding.stagedByAssetId.has(asset.assetId)) {
      flags.appendChild(makeAssetFlag("Уже в моде", "asset-flag-good"));
    }

    card.addEventListener("click", () => {
      state.modding.selectedAssetId = asset.assetId;
      state.modding.selectedAsset = asset;
      renderModAssetRows();
      loadSelectedAssetSchema().catch(showError);
    });

    card.append(top, summary, flags);
    host.appendChild(card);
  }

  renderSelectedAssetPreview();
  updateModAssetMeta();
  renderStudioFlowBar();
  renderModPaging();
}

function syncSchemaAfterAssetListChange() {
  if (!state.modding.selectedAssetId) {
    clearSchemaView();
    return;
  }

  if (state.modding.currentSchema?.assetId === state.modding.selectedAssetId) {
    renderSelectedAssetPreview();
    return;
  }

  showDeferredSchemaView(state.modding.selectedAsset);
}

async function loadModdingAssets() {
  const search = encodeURIComponent(el("modAssetSearch").value.trim());
  const categoryId = encodeURIComponent(state.modding.selectedCategoryId);
  state.modding.pageSize = Math.max(40, toIntSafe(el("modPageSize").value, 40));

  const url =
    `/api/modding/assets?categoryId=${categoryId}` +
    `&search=${search}` +
    `&page=${state.modding.page}` +
    `&pageSize=${state.modding.pageSize}`;

  const payload = await api(url);
  state.modding.assets = Array.isArray(payload?.items) ? payload.items : [];
  state.modding.total = toIntSafe(payload?.total, 0);
  state.modding.page = Math.max(1, toIntSafe(payload?.page, state.modding.page));
  state.modding.pageSize = Math.max(40, toIntSafe(payload?.pageSize, state.modding.pageSize));

  renderCategoryChips();
  syncSelectedAssetWithVisibleList();
  renderModAssetRows();
  syncSchemaAfterAssetListChange();
}

function clearSchemaView() {
  state.modding.currentSchema = null;
  state.modding.currentFieldValues = new Map();
  state.modding.currentFieldDisplayValues = new Map();
  state.modding.currentOriginalValues = new Map();
  state.modding.currentListEdits = [];
  state.modding.currentScene = null;
  state.modding.currentSceneSelectionId = "";
  state.modding.currentSceneDrag = null;
  state.modding.currentSceneFilterKind = "all";
  state.modding.currentSceneSearch = "";
  state.modding.currentSceneFocusMode = "all";
  state.modding.modelTargetLongestTouched = false;
  state.modding.schemaFieldFilter = "";
  el("schemaAssetTitle").textContent = "Раздел не выбран";
  el("schemaAssetSummary").textContent = "";
  el("schemaMeta").textContent = "";
  el("schemaWarnings").innerHTML = "";
  el("schemaSections").innerHTML = "";
  el("listTargetRows").innerHTML = "";
  syncVehicleAdapterControls(null);
  syncWeaponAdapterControls(null);
  if (el("schemaFieldFilter")) {
    el("schemaFieldFilter").value = "";
  }
  if (el("schemaFilterMeta")) {
    el("schemaFilterMeta").textContent = "";
  }
  if (el("sceneTypeFilter")) {
    el("sceneTypeFilter").value = "all";
  }
  if (el("sceneSearchInput")) {
    el("sceneSearchInput").value = "";
  }
  if (el("sceneFocusMode")) {
    el("sceneFocusMode").value = "all";
  }
  renderCurrentListOps();
  renderSceneEditor();
  renderSelectedAssetPreview();
  clearVehicleProfilePanel();
  renderRawModelAnalysisPanel();
  refreshModelReplacementWizard();
}

function showDeferredSchemaView(asset) {
  state.modding.currentSchema = null;
  state.modding.currentFieldValues = new Map();
  state.modding.currentFieldDisplayValues = new Map();
  state.modding.currentOriginalValues = new Map();
  state.modding.currentListEdits = [];
  state.modding.currentScene = null;
  state.modding.currentSceneSelectionId = "";
  state.modding.currentSceneDrag = null;
  state.modding.currentSceneFilterKind = "all";
  state.modding.currentSceneSearch = "";
  state.modding.currentSceneFocusMode = "all";
  state.modding.schemaFieldFilter = "";
  clearVehicleProfilePanel();

  el("schemaAssetTitle").textContent = asset?.displayName || "Раздел выбран";
  el("schemaAssetSummary").textContent = asset?.summary || "";
  el("schemaMeta").textContent = "Настройки ещё не загружены.";
  el("schemaWarnings").innerHTML = "";
  el("schemaSections").innerHTML = '<div class="schema-loading muted">Нажми на карточку системы или кнопку обновления, чтобы прочитать настройки из игры.</div>';
  el("listTargetRows").innerHTML = "";
  if (el("schemaFieldFilter")) {
    el("schemaFieldFilter").value = "";
  }
  if (el("schemaFilterMeta")) {
    el("schemaFilterMeta").textContent = "";
  }
  clearScenePanelContent();
  renderCurrentListOps();
  renderSelectedAssetPreview();
  refreshModelReplacementWizard();
}

function clearScenePanelContent() {
  state.modding.currentScene = null;
  state.modding.currentSceneSelectionId = "";
  state.modding.currentSceneDrag = null;
  const panel = el("sceneEditorPanel");
  const viewport = el("sceneViewport");
  const selection = el("sceneSelection");
  const empty = el("sceneViewportEmpty");
  const hint = el("sceneEditorHint");
  const meta = el("sceneEditorMeta");
  if (panel) {
    panel.hidden = true;
  }
  if (viewport) {
    viewport.innerHTML = "";
  }
  if (selection) {
    selection.innerHTML = "";
  }
  if (empty) {
    empty.hidden = true;
    empty.textContent = "";
  }
  if (hint) {
    hint.textContent = "";
  }
  if (meta) {
    meta.textContent = "";
  }
}

function renderSchemaWarnings(warnings) {
  const host = el("schemaWarnings");
  host.innerHTML = "";
  if (!Array.isArray(warnings) || !warnings.length) {
    return;
  }

  for (const warningText of warnings) {
    const item = document.createElement("div");
    item.className = "warning-item";
    item.textContent = warningText;
    host.appendChild(item);
  }
}

function getSchemaFilterTerm() {
  return String(state.modding.schemaFieldFilter || "").trim().toLowerCase();
}

function schemaFieldMatchesFilter(field, filterTerm) {
  if (!filterTerm) {
    return true;
  }

  const haystack = [
    field?.label,
    field?.description,
    field?.section,
    field?.currentDisplayValue,
    field?.currentValue
  ]
    .filter(Boolean)
    .join(" \n")
    .toLowerCase();
  return haystack.includes(filterTerm);
}

function schemaListTargetMatchesFilter(target, filterTerm) {
  if (!filterTerm) {
    return true;
  }

  const entryLabels = Array.isArray(target?.entryLabels) ? target.entryLabels.join(" \n") : "";
  const haystack = [
    target?.label,
    target?.description,
    entryLabels
  ]
    .filter(Boolean)
    .join(" \n")
    .toLowerCase();
  return haystack.includes(filterTerm);
}

function getFilteredSchemaFields(schema) {
  const fields = Array.isArray(schema?.fields) ? schema.fields : [];
  const filterTerm = getSchemaFilterTerm();
  return fields.filter((field) => schemaFieldMatchesFilter(field, filterTerm));
}

function getFilteredListTargets(schema) {
  const listTargets = Array.isArray(schema?.listTargets) ? schema.listTargets : [];
  const filterTerm = getSchemaFilterTerm();
  return listTargets.filter((target) => schemaListTargetMatchesFilter(target, filterTerm));
}

function renderSchemaFilterMeta() {
  const host = el("schemaFilterMeta");
  if (!host) {
    return;
  }

  const schema = state.modding.currentSchema;
  const allFields = Array.isArray(schema?.fields) ? schema.fields : [];
  const allTargets = Array.isArray(schema?.listTargets) ? schema.listTargets : [];
  const filterTerm = getSchemaFilterTerm();

  if (!schema) {
    host.textContent = "";
    return;
  }

  if (!filterTerm) {
    host.textContent = `Настроек: ${allFields.length}. Блоков состава и связей: ${allTargets.length}.`;
    return;
  }

  const visibleFields = getFilteredSchemaFields(schema).length;
  const visibleTargets = getFilteredListTargets(schema).length;
  host.textContent = `По запросу «${filterTerm}»: настроек ${visibleFields}, блоков состава ${visibleTargets}.`;
}

function setSchemaMeta(text) {
  el("schemaMeta").textContent = text;
}

function applyFieldEditableState(field, element) {
  if (field?.editable !== false) {
    return element;
  }

  if (typeof element.matches === "function" && element.matches("input, select, button, textarea")) {
    element.disabled = true;
  }

  const controls = typeof element.querySelectorAll === "function"
    ? element.querySelectorAll("input, select, button, textarea")
    : [];
  for (const control of controls) {
    control.disabled = true;
  }

  element.classList?.add("is-readonly");
  return element;
}

function createFieldInput(field) {
  const currentValue = state.modding.currentFieldValues.get(field.fieldPath) ?? field.currentValue;
  let input;
  if (field.editorKind === "toggle" || field.valueType === "bool") {
    input = document.createElement("input");
    input.type = "checkbox";
    input.checked = String(currentValue).toLowerCase() === "true";
    input.addEventListener("change", () => {
      setCurrentFieldValue(field.fieldPath, input.checked ? "true" : "false");
    });
    return applyFieldEditableState(field, attachFieldInputMeta(input, field));
  }

  if (field.editorKind === "number") {
    input = document.createElement("input");
    input.type = "number";
    input.step = field.valueType === "float" || field.valueType === "double" ? "0.01" : "1";
    if (field.suggestedMin !== null && field.suggestedMin !== undefined) {
      input.min = String(field.suggestedMin);
    }
    if (field.suggestedMax !== null && field.suggestedMax !== undefined) {
      input.max = String(field.suggestedMax);
    }
    input.value = currentValue;
    input.addEventListener("input", () => {
      setCurrentFieldValue(field.fieldPath, input.value);
    });
    return applyFieldEditableState(field, attachFieldInputMeta(input, field));
  }

  if (field.editorKind === "select" || Array.isArray(field.options)) {
    input = document.createElement("select");
    input.className = "field-input";

    const options = Array.isArray(field.options) ? field.options : [];
    for (const option of options) {
      const node = document.createElement("option");
      node.value = option.value;
      node.textContent = option.label || option.value;
      input.appendChild(node);
    }

    if (!options.some((option) => String(option.value) === String(currentValue))) {
      const fallback = document.createElement("option");
      fallback.value = currentValue;
      fallback.textContent = currentValue || "не выбрано";
      input.appendChild(fallback);
    }

    input.value = currentValue;
    input.addEventListener("change", () => {
      setCurrentFieldValue(field.fieldPath, input.value);
    });
    return applyFieldEditableState(field, attachFieldInputMeta(input, field));
  }

  if (field.editorKind === "item-picker") {
    const wrap = document.createElement("div");
    wrap.className = "picker-field";

    const current = document.createElement("div");
    current.className = "picker-current small";
    current.textContent = `Сейчас выбрано: ${getCurrentFieldDisplayValue(field, currentValue)}`;

    const search = document.createElement("input");
    search.type = "text";
    search.placeholder = field.editable === false ? "Редактирование отключено" : "Введи хотя бы 2 буквы, чтобы найти предмет";
    search.className = "field-input";

    const results = document.createElement("div");
    results.className = "picker-results";

    let requestToken = 0;
    search.addEventListener("input", async () => {
      const term = search.value.trim();
      const myToken = ++requestToken;
      results.innerHTML = "";

      if (term.length < 2) {
        return;
      }

      try {
        const payload = await api(`/api/catalog/search?term=${encodeURIComponent(term)}&limit=10`);
        if (myToken !== requestToken) {
          return;
        }

        const items = Array.isArray(payload?.items) ? payload.items : [];
        for (const item of items) {
          const row = document.createElement("button");
          row.type = "button";
          row.className = "picker-result";

          if (item.iconUrl) {
            const icon = document.createElement("img");
            icon.src = item.iconUrl;
            icon.alt = "";
            icon.className = "picker-icon";
            row.appendChild(icon);
          }

          const text = document.createElement("span");
          text.textContent = item.itemName || item.itemId;
          row.appendChild(text);

          row.addEventListener("click", () => {
            const softRef = buildItemClassRef(item);
            setCurrentFieldValue(field.fieldPath, softRef, { renderScene: false });
            state.modding.currentFieldDisplayValues.set(field.fieldPath, item.itemName || referenceValueToReadableName(softRef));
            current.textContent = `Сейчас выбрано: ${item.itemName || softObjectToReadableName(softRef)}`;
            search.value = "";
            results.innerHTML = "";
          });

          results.appendChild(row);
        }
      } catch (error) {
        if (myToken !== requestToken) {
          return;
        }

        const fail = document.createElement("div");
        fail.className = "small muted";
        fail.textContent = error.message || "Не удалось загрузить список предметов.";
        results.appendChild(fail);
      }
    });

    wrap.append(current, search, results);
    return applyFieldEditableState(field, wrap);
  }

  if (field.editorKind === "reference-picker" && field.referencePickerKind) {
    const wrap = document.createElement("div");
    wrap.className = "picker-field";

    const current = document.createElement("div");
    current.className = "picker-current small";
    current.textContent = `Сейчас выбрано: ${getCurrentFieldDisplayValue(field, currentValue)}`;

    const search = document.createElement("input");
    search.type = "text";
    search.placeholder = field.editable === false
      ? "Редактирование отключено"
      : buildReferenceSearchPlaceholder(field.referencePickerKind, field.referencePickerPrompt || "Введи хотя бы 2 буквы для поиска");
    search.className = "field-input";

    const pickerToolbar = document.createElement("div");
    pickerToolbar.className = "picker-toolbar";

    const showOptionsBtn = document.createElement("button");
    showOptionsBtn.type = "button";
    showOptionsBtn.textContent = "Показать варианты";
    showOptionsBtn.addEventListener("click", () => {
      refreshReferenceResults();
      search.focus();
    });

    const results = document.createElement("div");
    results.className = "picker-results";

    const quickHints = getFieldQuickPickerHints(field);
    if (quickHints.length) {
      const quickRow = document.createElement("div");
      quickRow.className = "quick-action-row";
      quickHints.forEach((hint) => {
        const chip = document.createElement("button");
        chip.type = "button";
        chip.className = "quick-action-chip";
        chip.textContent = hint.label;
        chip.addEventListener("click", () => {
          search.value = hint.term;
          refreshReferenceResults();
        });
        quickRow.appendChild(chip);
      });
      wrap.appendChild(quickRow);
    }

    const help = document.createElement("div");
    help.className = "picker-help small muted";
    help.textContent = buildPickerIntroText(field.referencePickerKind, quickHints.length > 0, true);

    let requestToken = 0;
    async function refreshReferenceResults() {
      const term = search.value.trim();
      const myToken = ++requestToken;
      results.innerHTML = "";

      try {
        const payload = await fetchReferenceOptions(field.referencePickerKind, term, term ? 10 : 8);
        if (myToken !== requestToken) {
          return;
        }

        const options = Array.isArray(payload) ? payload : [];
        if (!options.length) {
          const empty = document.createElement("div");
          empty.className = "small muted";
          empty.textContent = buildPickerIntroText(field.referencePickerKind, quickHints.length > 0, false);
          results.appendChild(empty);
          return;
        }

        for (const option of options) {
          const row = document.createElement("button");
          row.type = "button";
          row.className = "picker-result";

          const text = document.createElement("span");
          text.textContent = `${buildReferenceActionLabel(field.referencePickerKind, "choose")}: ${option.label || referenceValueToReadableName(option.value)}`;
          row.appendChild(text);

          row.addEventListener("click", () => {
            setCurrentFieldValue(field.fieldPath, option.value, { renderScene: false });
            state.modding.currentFieldDisplayValues.set(field.fieldPath, option.label || referenceValueToReadableName(option.value));
            current.textContent = `Сейчас выбрано: ${option.label || referenceValueToReadableName(option.value)}`;
            search.value = "";
            results.innerHTML = "";
          });

          results.appendChild(row);
        }
      } catch (error) {
        if (myToken !== requestToken) {
          return;
        }

        const fail = document.createElement("div");
        fail.className = "small muted";
        fail.textContent = error.message || "Не удалось загрузить игровой список.";
        results.appendChild(fail);
      }
    }

    search.addEventListener("focus", () => {
      if (!results.childElementCount) {
        refreshReferenceResults();
      }
    });
    search.addEventListener("input", () => {
      refreshReferenceResults();
    });

    pickerToolbar.append(search, showOptionsBtn);
    wrap.append(current, help, pickerToolbar, results);
    return applyFieldEditableState(field, wrap);
  }

  input = document.createElement("input");
  input.type = "text";
  input.value = currentValue;
  input.addEventListener("input", () => {
    setCurrentFieldValue(field.fieldPath, input.value);
  });
  return applyFieldEditableState(field, attachFieldInputMeta(input, field));
}

function buildFieldRow(field) {
  const row = document.createElement("div");
  row.className = "field-row";
  if (field.editable === false) {
    row.classList.add("field-row-readonly");
  }

  const left = document.createElement("div");
  left.className = "field-left";

  const name = document.createElement("div");
  name.className = "field-name";
  name.textContent = field.label;

  const hint = document.createElement("div");
  hint.className = "small muted";
  hint.textContent = field.editable === false
    ? (field.description || "Справочная связь из игры.")
    : (field.description || "Безопасный параметр.");

  left.append(name, hint);

  const right = document.createElement("div");
  right.className = "field-right";
  const input = createFieldInput(field);
  if (!input.className) {
    input.className = "field-input";
  }
  right.appendChild(input);

  row.append(left, right);
  return row;
}

function parseCurveField(field) {
  const label = String(field?.label || "").trim();
  const match = label.match(/^(.*)\s\/\sточка\s(\d+)\s\/\s(когда начинается эта ступень|насколько сильно действует эта ступень)$/i);
  if (!match) {
    return null;
  }

  return {
    groupLabel: match[1].trim(),
    pointIndex: Math.max(1, Number(match[2] || 1)),
    metricLabel: match[3].trim(),
    metricKind: match[3].toLowerCase().includes("когда начинается")
      ? "threshold"
      : "value"
  };
}

function getCurveStageName(index, total) {
  if (total === 1) {
    return "Единственная стадия";
  }

  if (total === 2) {
    return index === 1 ? "Начало эффекта" : "Предел эффекта";
  }

  if (total === 3) {
    return ["Лёгкая стадия", "Средняя стадия", "Тяжёлая стадия"][index - 1] || `Стадия ${index}`;
  }

  if (total === 4) {
    return ["Лёгкая стадия", "Средняя стадия", "Сильная стадия", "Крайняя стадия"][index - 1] || `Стадия ${index}`;
  }

  if (total === 5) {
    return ["Лёгкая стадия", "Нарастание", "Средняя стадия", "Тяжёлая стадия", "Критическая стадия"][index - 1] || `Стадия ${index}`;
  }

  return `Стадия ${index}`;
}

function describeCurveGroup(label) {
  const text = String(label || "").toLowerCase();
  if (text.includes("алкогол") || text.includes("уровень алкоголя")) {
    return "Показывает, как эффект усиливается по мере накопления алкоголя в организме.";
  }
  if (text.includes("шанс рвоты")) {
    return "Показывает, насколько вероятна рвота на каждой стадии эффекта.";
  }
  if (text.includes("потеря здоровья") || text.includes("урон")) {
    return "Показывает, сколько здоровья будет терять персонаж на каждой стадии.";
  }
  if (text.includes("дезориентация")) {
    return "Показывает, насколько сильно персонажа будет шатать и путать.";
  }
  if (text.includes("двоение в глазах")) {
    return "Показывает, насколько сильно будет двоиться изображение у персонажа.";
  }
  if (text.includes("скорости ходьбы и бега")) {
    return "Показывает, насколько персонаж ускорится или замедлится при движении по земле.";
  }
  if (text.includes("скорости плавания")) {
    return "Показывает, насколько персонаж ускорится или замедлится при плавании.";
  }
  if (text.includes("интеллект")) {
    return "Показывает, как эта стадия меняет интеллект персонажа.";
  }
  if (text.includes("силе") || text.includes("сила")) {
    return "Показывает, как эта стадия меняет физическую силу персонажа.";
  }
  if (text.includes("выносливости")) {
    return "Показывает, как эта стадия меняет запас или расход выносливости.";
  }
  if (text.includes("периодический приступ") && text.includes("интервал")) {
    return "Показывает, как часто на этой стадии может повторяться приступ.";
  }
  if (text.includes("периодический приступ") && text.includes("шанс")) {
    return "Показывает, насколько вероятен приступ на этой стадии.";
  }
  if (text.includes("максимум вещества по телосложению")) {
    return "Показывает, как телосложение увеличивает или уменьшает предел вещества в организме.";
  }
  if (text.includes("выведение")) {
    return "Показывает, как быстро вещество будет выводиться на каждой стадии этой кривой.";
  }

  return "Показывает, как система меняется от лёгкой стадии к тяжёлой.";
}

function renderCurveGroups(host, fields) {
  const grouped = new Map();
  const fallback = [];

  for (const field of fields) {
    const parsed = parseCurveField(field);
    if (!parsed) {
      fallback.push(field);
      continue;
    }

    if (!grouped.has(parsed.groupLabel)) {
      grouped.set(parsed.groupLabel, []);
    }
    grouped.get(parsed.groupLabel).push({ field, parsed });
  }

  for (const [groupLabel, entries] of grouped.entries()) {
    const card = document.createElement("div");
    card.className = "curve-group";

    const title = document.createElement("div");
    title.className = "curve-group-title";
    title.textContent = groupLabel;

    const summary = document.createElement("div");
    summary.className = "curve-group-summary";
    summary.textContent = describeCurveGroup(groupLabel);

    const note = document.createElement("div");
    note.className = "curve-group-note";
    note.textContent = "Чем выше число в поле «Когда включается», тем позже начинается эта стадия.";

    const stageGrid = document.createElement("div");
    stageGrid.className = "curve-stage-grid";

    const byPoint = new Map();
    for (const entry of entries) {
      if (!byPoint.has(entry.parsed.pointIndex)) {
        byPoint.set(entry.parsed.pointIndex, { threshold: null, value: null, extra: [] });
      }

      const slot = byPoint.get(entry.parsed.pointIndex);
      if (entry.parsed.metricKind === "threshold") {
        slot.threshold = entry.field;
      } else if (entry.parsed.metricKind === "value") {
        slot.value = entry.field;
      } else {
        slot.extra.push(entry.field);
      }
    }

    const orderedPoints = Array.from(byPoint.entries()).sort((a, b) => a[0] - b[0]);
    const total = orderedPoints.length;
    for (const [pointIndex, stage] of orderedPoints) {
      const stageCard = document.createElement("div");
      stageCard.className = "curve-stage-card";

      const stageTitle = document.createElement("div");
      stageTitle.className = "curve-stage-title";
      stageTitle.textContent = getCurveStageName(pointIndex, total);

      const stageSub = document.createElement("div");
      stageSub.className = "curve-stage-subtitle";
      stageSub.textContent = `Точка ${pointIndex} из ${total}`;

      stageCard.append(stageTitle, stageSub);

      const orderedFields = [stage.threshold, stage.value, ...(stage.extra || [])].filter(Boolean);
      orderedFields.forEach((curveField) => {
        const compactField = {
          ...curveField,
          label: parseCurveField(curveField)?.metricKind === "threshold"
            ? "Когда включается эта стадия"
            : parseCurveField(curveField)?.metricKind === "value"
              ? "Насколько сильно действует эта стадия"
              : curveField.label
        };
        stageCard.appendChild(buildFieldRow(compactField));
      });

      stageGrid.appendChild(stageCard);
    }

    card.append(title, summary, note, stageGrid);
    host.appendChild(card);
  }

  fallback.forEach((field) => {
    host.appendChild(buildFieldRow(field));
  });
}

function appendFieldSections(host, fields) {
  const groups = new Map();
  for (const field of fields) {
    const key = field.section || "Общие";
    if (!groups.has(key)) {
      groups.set(key, []);
    }
    groups.get(key).push(field);
  }

  const orderedGroups = Array.from(groups.entries()).sort((a, b) => sectionPriority(a[0]) - sectionPriority(b[0]));
  orderedGroups.forEach(([sectionName, sectionFields], index) => {
    const section = document.createElement("details");
    section.className = "schema-section";
    section.open = orderedGroups.length === 1 || index === 0;

    const title = document.createElement("summary");
    title.className = "schema-section-title";
    title.textContent = `${sectionName} (${sectionFields.length})`;
    section.appendChild(title);

    const body = document.createElement("div");
    body.className = "schema-section-body";

    if (sectionName === "Кривые эффекта") {
      renderCurveGroups(body, sectionFields);
    } else {
      for (const field of sectionFields) {
        body.appendChild(buildFieldRow(field));
      }
    }

    section.appendChild(body);
    host.appendChild(section);
  });
}

function buildSceneLegend(nodes) {
  const legend = document.createElement("div");
  legend.className = "scene-legend";
  const seen = new Set();
  nodes.forEach((node) => {
    if (seen.has(node.kind)) {
      return;
    }

    seen.add(node.kind);
    const chip = document.createElement("div");
    chip.className = "scene-legend-chip";

    const dot = document.createElement("span");
    dot.className = "scene-legend-dot";
    dot.style.setProperty("--scene-color", node.color);

    const text = document.createElement("span");
    text.textContent = sceneKindTitle(node.kind);
    chip.append(dot, text);
    legend.appendChild(chip);
  });
  return legend;
}

function nudgeSelectedSceneNode(dx, dy, dz = 0) {
  const scene = state.modding.currentScene;
  const selectedNode = scene?.nodes.find((node) => node.id === state.modding.currentSceneSelectionId) || null;
  if (!selectedNode) {
    return;
  }

  const step = Number(state.modding.currentSceneNudgeStep || 25);
  if (dx && selectedNode.fieldPaths.x) {
    setCurrentFieldValue(selectedNode.fieldPaths.x, formatSceneFieldValue(selectedNode.x + dx * step), { renderScene: false });
  }
  if (dy && selectedNode.fieldPaths.y) {
    setCurrentFieldValue(selectedNode.fieldPaths.y, formatSceneFieldValue(selectedNode.y + dy * step), { renderScene: false });
  }
  if (dz && selectedNode.fieldPaths.z) {
    setCurrentFieldValue(selectedNode.fieldPaths.z, formatSceneFieldValue(selectedNode.z + dz * step), { renderScene: false });
  }

  renderSceneEditor();
}

function renderSceneSelection(scene) {
  const host = el("sceneSelection");
  host.innerHTML = "";
  const visibleNodes = Array.isArray(scene.visibleNodes) ? scene.visibleNodes : scene.nodes;
  const selectionId = state.modding.currentSceneSelectionId;
  const selectedNode = scene.nodes.find((node) => node.id === selectionId) || null;
  if (!selectedNode) {
    const empty = document.createElement("div");
    empty.className = "scene-selection-empty";

    const title = document.createElement("div");
    title.className = "scene-selection-title";
    title.textContent = "Выбери точку на сцене";

    const note = document.createElement("div");
    note.className = "scene-selection-note";
    note.textContent = visibleNodes.length
      ? "Сцена показывает только поддерживаемые игровые точки. Выбери нужную точку из списка ниже или кликни по ней прямо на карте."
      : "По текущему фильтру или поиску ничего не найдено. Попробуй снять ограничение или ввести другое игровое слово.";

    empty.append(title, note, buildSceneLegend(scene.nodes));
    host.appendChild(empty);
  } else {
    const titleRow = document.createElement("div");
    titleRow.className = "scene-selection-title-row";

    const title = document.createElement("div");
    title.className = "scene-selection-title";
    title.textContent = buildSceneFriendlyName(selectedNode);

    const badge = document.createElement("div");
    badge.className = "scene-selection-badge";
    badge.textContent = sceneKindTitle(selectedNode.kind);

    titleRow.append(title, badge);
    host.appendChild(titleRow);

    const rawLabel = document.createElement("div");
    rawLabel.className = "small muted";
    rawLabel.textContent = selectedNode.label;
    host.appendChild(rawLabel);

    const hint = document.createElement("div");
    hint.className = "scene-selection-note";
    hint.textContent = selectedNode.editable
      ? describeSceneNodeUsage(selectedNode)
      : "Эта точка сейчас доступна только для просмотра. Состав системы можно менять ниже, а затем открыть новый результат.";
    host.appendChild(hint);

    const grid = document.createElement("div");
    grid.className = "scene-selection-grid";

    function appendNumberEditor(label, value, fieldPath) {
      if (!fieldPath) {
        return;
      }

      const row = document.createElement("label");
      row.className = "scene-input-row";

      const caption = document.createElement("span");
      caption.textContent = label;

      const input = document.createElement("input");
      input.type = "number";
      input.step = "0.1";
      input.value = formatSceneFieldValue(value);
      input.addEventListener("change", () => {
        setCurrentFieldValue(fieldPath, input.value);
      });

      row.append(caption, input);
      grid.appendChild(row);
    }

    appendNumberEditor("X", selectedNode.x, selectedNode.fieldPaths.x);
    appendNumberEditor("Y", selectedNode.y, selectedNode.fieldPaths.y);
    appendNumberEditor("Z", selectedNode.z, selectedNode.fieldPaths.z);
    appendNumberEditor("Yaw", selectedNode.yaw, selectedNode.rotationFieldPaths.yaw);
    appendNumberEditor("Pitch", selectedNode.pitch, selectedNode.rotationFieldPaths.pitch);
    appendNumberEditor("Roll", selectedNode.roll, selectedNode.rotationFieldPaths.roll);
    host.appendChild(grid);

    if (selectedNode.editable) {
      const nudgeBox = document.createElement("div");
      nudgeBox.className = "scene-nudge-box";

      const nudgeTitle = document.createElement("div");
      nudgeTitle.className = "scene-nudge-title";
      nudgeTitle.textContent = "Точная подстройка";

      const toolbar = document.createElement("div");
      toolbar.className = "scene-nudge-toolbar";

      const stepLabel = document.createElement("label");
      stepLabel.className = "scene-input-row";
      const stepCaption = document.createElement("span");
      stepCaption.textContent = "Шаг смещения";
      const stepSelect = document.createElement("select");
      [10, 25, 50, 100].forEach((value) => {
        const option = document.createElement("option");
        option.value = String(value);
        option.textContent = `${value} ед.`;
        if (value === Number(state.modding.currentSceneNudgeStep || 25)) {
          option.selected = true;
        }
        stepSelect.appendChild(option);
      });
      stepSelect.addEventListener("change", () => {
        state.modding.currentSceneNudgeStep = Number(stepSelect.value || 25);
      });
      stepLabel.append(stepCaption, stepSelect);
      toolbar.appendChild(stepLabel);

      const pad = document.createElement("div");
      pad.className = "scene-nudge-pad";
      [
        [{ label: "↑", dx: 0, dy: 1 }],
        [{ label: "←", dx: -1, dy: 0 }, { label: "→", dx: 1, dy: 0 }],
        [{ label: "↓", dx: 0, dy: -1 }]
      ].forEach((rowButtons) => {
        const row = document.createElement("div");
        row.className = "scene-nudge-row";
        rowButtons.forEach((buttonConfig) => {
          const button = document.createElement("button");
          button.type = "button";
          button.textContent = buttonConfig.label;
          button.addEventListener("click", () => nudgeSelectedSceneNode(buttonConfig.dx, buttonConfig.dy, 0));
          row.appendChild(button);
        });
        pad.appendChild(row);
      });

      const zRow = document.createElement("div");
      zRow.className = "scene-nudge-row";
      const zDown = document.createElement("button");
      zDown.type = "button";
      zDown.textContent = "Z−";
      zDown.addEventListener("click", () => nudgeSelectedSceneNode(0, 0, -1));
      const zUp = document.createElement("button");
      zUp.type = "button";
      zUp.textContent = "Z+";
      zUp.addEventListener("click", () => nudgeSelectedSceneNode(0, 0, 1));
      zRow.append(zDown, zUp);
      pad.appendChild(zRow);

      nudgeBox.append(nudgeTitle, toolbar, pad);
      host.appendChild(nudgeBox);
    }
  }

  const listTitle = document.createElement("div");
  listTitle.className = "scene-nudge-title";
  listTitle.textContent = `Быстрый выбор точки (${visibleNodes.length})`;
  host.appendChild(listTitle);

  const list = document.createElement("div");
  list.className = "scene-node-list";
  visibleNodes.slice(0, 48).forEach((node) => {
    const item = document.createElement("button");
    item.type = "button";
    item.className = "scene-node-item";
    if (node.id === selectionId) {
      item.classList.add("is-active");
    }

    const title = document.createElement("div");
    title.className = "scene-node-item-title";
    title.textContent = buildSceneFriendlyName(node);

    const meta = document.createElement("div");
    meta.className = "scene-node-item-meta";
    meta.textContent = `${sceneKindTitle(node.kind)} · X ${formatSceneNumber(node.x)} · Y ${formatSceneNumber(node.y)}`;

    item.append(title, meta);
    item.addEventListener("click", () => {
      state.modding.currentSceneSelectionId = node.id;
      state.modding.currentSceneFocusMode = "selected";
      if (el("sceneFocusMode")) {
        el("sceneFocusMode").value = "selected";
      }
      renderSceneEditor();
    });
    list.appendChild(item);
  });
  host.appendChild(list);

  if (visibleNodes.length > 48) {
    const more = document.createElement("div");
    more.className = "small muted";
    more.textContent = "Показаны первые 48 точек. Чтобы сузить список, используй фильтр по типу или поиск выше.";
    host.appendChild(more);
  }
}

function renderSceneViewport(scene) {
  const svg = el("sceneViewport");
  svg.innerHTML = "";
  svg.setAttribute("viewBox", `0 0 ${SCENE_VIEWBOX_WIDTH} ${SCENE_VIEWBOX_HEIGHT}`);

  const bg = svgElement("rect", {
    x: 0,
    y: 0,
    width: SCENE_VIEWBOX_WIDTH,
    height: SCENE_VIEWBOX_HEIGHT,
    fill: "transparent"
  });
  svg.appendChild(bg);

  const gridGroup = svgElement("g");
  for (let index = 0; index <= 10; index += 1) {
    const x = (SCENE_VIEWBOX_WIDTH / 10) * index;
    const y = (SCENE_VIEWBOX_HEIGHT / 10) * index;
    gridGroup.appendChild(svgElement("line", {
      x1: x,
      y1: 0,
      x2: x,
      y2: SCENE_VIEWBOX_HEIGHT,
      stroke: "#213244",
      "stroke-width": 1
    }));
    gridGroup.appendChild(svgElement("line", {
      x1: 0,
      y1: y,
      x2: SCENE_VIEWBOX_WIDTH,
      y2: y,
      stroke: "#213244",
      "stroke-width": 1
    }));
  }
  svg.appendChild(gridGroup);

  const visibleNodes = Array.isArray(scene.visibleNodes) ? scene.visibleNodes : scene.nodes;
  const visibleLinks = Array.isArray(scene.visibleLinks) ? scene.visibleLinks : scene.links;
  const nodeById = new Map(scene.nodes.map((node) => [node.id, node]));
  const linkGroup = svgElement("g");
  visibleLinks.forEach((link) => {
    const from = nodeById.get(link.fromId);
    const to = nodeById.get(link.toId);
    if (!from || !to) {
      return;
    }

    const fromPoint = projectScenePoint(scene, from.x, from.y);
    const toPoint = projectScenePoint(scene, to.x, to.y);
    linkGroup.appendChild(svgElement("line", {
      x1: fromPoint.x,
      y1: fromPoint.y,
      x2: toPoint.x,
      y2: toPoint.y,
      stroke: from.color,
      "stroke-width": 3,
      "stroke-linecap": "round",
      opacity: 0.75
    }));
  });
  svg.appendChild(linkGroup);

  const selectedId = state.modding.currentSceneSelectionId;
  visibleNodes.forEach((node) => {
    const point = projectScenePoint(scene, node.x, node.y);
    const group = svgElement("g", { cursor: node.editable ? "grab" : "default" });
    const outerRadius = node.id === selectedId ? 15 : 11;
    const innerRadius = node.id === selectedId ? 10 : 8;

    const halo = svgElement("circle", {
      cx: point.x,
      cy: point.y,
      r: outerRadius,
      fill: node.id === selectedId ? `${node.color}22` : "#081019",
      stroke: node.id === selectedId ? "#ffffff" : "#29415c",
      "stroke-width": node.id === selectedId ? 2.2 : 1.3
    });

    const body = svgElement("circle", {
      cx: point.x,
      cy: point.y,
      r: innerRadius,
      fill: node.color,
      stroke: "#08111a",
      "stroke-width": 1.5
    });

    group.append(halo, body);

    if (node.badge && node.badge !== "•") {
      const text = svgElement("text", {
        x: point.x,
        y: point.y + 3.5,
        "text-anchor": "middle",
        "font-size": node.badge.length > 2 ? 8.5 : 10.5,
        "font-family": "Segoe UI, Arial, sans-serif",
        "font-weight": 700,
        fill: "#071018"
      });
      text.textContent = node.badge;
      group.appendChild(text);
    }

    const title = svgElement("title");
    title.textContent = `${selectedId === node.id ? "Выбрано" : "Точка"}: ${buildSceneFriendlyName(node)}\nX: ${formatSceneNumber(node.x)} | Y: ${formatSceneNumber(node.y)} | Z: ${formatSceneNumber(node.z)}`;
    group.appendChild(title);

    group.addEventListener("pointerdown", (event) => {
      event.preventDefault();
      state.modding.currentSceneSelectionId = node.id;
      if (node.editable) {
        const world = scenePointerToWorld(svg, event.clientX, event.clientY, scene);
        state.modding.currentSceneDrag = {
          nodeId: node.id,
          pointerId: event.pointerId,
          offsetX: node.x - world.x,
          offsetY: node.y - world.y,
          view: {
            bounds: { ...scene.bounds },
            projection: { ...scene.projection }
          }
        };
      }
      renderSceneEditor();
    });

    svg.appendChild(group);

    if (node.id === selectedId || visibleNodes.length <= 10) {
      const label = svgElement("text", {
        x: point.x + 16,
        y: point.y - 16,
        "font-size": 12,
        "font-family": "Segoe UI, Arial, sans-serif",
        "font-weight": node.id === selectedId ? 700 : 500,
        fill: "#dcecff"
      });
      label.textContent = buildSceneFriendlyName(node);
      svg.appendChild(label);
    }
  });
}

function renderSceneEditor() {
  const panel = el("sceneEditorPanel");
  const meta = el("sceneEditorMeta");
  const hint = el("sceneEditorHint");
  const empty = el("sceneViewportEmpty");
  if (!panel || !meta || !hint || !empty) {
    return;
  }

  const schema = state.modding.currentSchema;
  if (!schema || !isMapSchema(schema)) {
    state.modding.currentScene = null;
    state.modding.currentSceneSelectionId = "";
    state.modding.currentSceneDrag = null;
    panel.hidden = true;
    return;
  }

  const scene = buildSceneModel(schema);
  state.modding.currentScene = scene;
  if (!scene || !scene.nodes.length) {
    state.modding.currentSceneSelectionId = "";
    state.modding.currentSceneDrag = null;
    panel.hidden = true;
    return;
  }

  panel.hidden = false;
  scene.visibleNodes = getVisibleSceneNodes(scene);
  if (!scene.visibleNodes.some((node) => node.id === state.modding.currentSceneSelectionId)) {
    state.modding.currentSceneSelectionId = scene.visibleNodes[0]?.id || "";
  }

  if (!scene.visibleNodes.length) {
    scene.visibleLinks = [];
    scene.bounds = computeSceneBounds([]);
    scene.projection = computeSceneProjection(scene.bounds);
    hint.textContent = "По этому фильтру сейчас нет видимых точек. Сними ограничение или попробуй другой поиск.";
    meta.textContent = `Всего найдено поддерживаемых точек: ${scene.nodes.length}.`;
    empty.hidden = false;
    empty.textContent = "По текущему фильтру и поиску ничего не найдено. Попробуй показать все точки или выбрать другой тип.";
    renderSceneViewport(scene);
    renderSceneSelection(scene);
    return;
  }

  const selectedNode = scene.nodes.find((node) => node.id === state.modding.currentSceneSelectionId) || null;
  const visibleIds = new Set(scene.visibleNodes.map((node) => node.id));
  scene.visibleLinks = scene.links.filter((link) => visibleIds.has(link.fromId) && visibleIds.has(link.toId));
  scene.bounds = state.modding.currentSceneDrag?.view?.bounds || computeSceneFocusBounds(scene.visibleNodes, selectedNode);
  scene.projection = state.modding.currentSceneDrag?.view?.projection || computeSceneProjection(scene.bounds);

  const routeCount = scene.visibleLinks.length > 0
    ? new Set(scene.visibleNodes.filter((node) => node.routeGroupKey).map((node) => node.routeGroupKey)).size
    : 0;
  hint.textContent = "Вид сверху. Используй фильтр и поиск, чтобы быстро найти нужную точку. Клик по списку справа автоматически приближает выбранную сущность.";
  meta.textContent = routeCount > 0
    ? `Показано ${scene.visibleNodes.length} точек из ${scene.nodes.length}. Маршрутов роботов в кадре: ${routeCount}.`
    : `Показано ${scene.visibleNodes.length} точек из ${scene.nodes.length}.`;

  empty.hidden = true;
  renderSceneViewport(scene);
  renderSceneSelection(scene);
}

function handleScenePointerMove(event) {
  const drag = state.modding.currentSceneDrag;
  const scene = state.modding.currentScene;
  if (!drag || !scene) {
    return;
  }

  const node = scene.nodes.find((entry) => entry.id === drag.nodeId);
  const svg = el("sceneViewport");
  if (!node || !svg) {
    return;
  }

  const world = scenePointerToWorld(svg, event.clientX, event.clientY, scene);
  const nextX = world.x + drag.offsetX;
  const nextY = world.y + drag.offsetY;

  if (node.fieldPaths.x) {
    setCurrentFieldValue(node.fieldPaths.x, formatSceneFieldValue(nextX), { renderScene: false });
  }

  if (node.fieldPaths.y) {
    setCurrentFieldValue(node.fieldPaths.y, formatSceneFieldValue(nextY), { renderScene: false });
  }

  renderSceneEditor();
}

function handleScenePointerUp() {
  if (!state.modding.currentSceneDrag) {
    return;
  }

  state.modding.currentSceneDrag = null;
  renderSceneEditor();
}

function renderSchemaFields() {
  const host = el("schemaSections");
  host.innerHTML = "";

  const schema = state.modding.currentSchema;
  const fields = getFilteredSchemaFields(schema);
  const filterTerm = getSchemaFilterTerm();
  if (!fields.length) {
    const guided = !filterTerm ? buildGuidedEmptyState(schema) : null;
    if (guided) {
      host.appendChild(guided);
    }

    const empty = document.createElement("div");
    empty.className = "muted";
    empty.textContent = filterTerm
      ? "По этому слову среди настроек ничего не найдено. Попробуй другое игровое слово: ресурс, отдача, количество, квест."
      : schemaActionableTargets(schema).length > 0
        ? "Сначала наполни систему нужными последствиями или связями, затем открой их новые настройки."
        : (schema?.listTargets?.length || 0) > 0
          ? "У этого раздела нет отдельных числовых настроек, но ниже можно менять состав связанных элементов."
          : "Для этого раздела пока нет понятных настроек, которые можно безопасно менять в студии.";
    host.appendChild(empty);
    renderSchemaFilterMeta();
    return;
  }

  const editableFields = fields.filter((field) => field.editable !== false);
  const readonlyFields = fields.filter((field) => field.editable === false);

  if (editableFields.length > 0) {
    appendFieldSections(host, editableFields);
  }

  if (!editableFields.length && readonlyFields.length) {
    const note = document.createElement("div");
    note.className = "schema-note";
    note.textContent = "В этом разделе нет безопасных прямых настроек, но ниже показано, с какими игровыми эффектами и объектами он связан.";
    host.appendChild(note);
  }

  if (readonlyFields.length > 0) {
    const details = document.createElement("details");
    details.className = "advanced-box";
    details.open = editableFields.length === 0;

    const summary = document.createElement("summary");
    summary.textContent = editableFields.length
      ? `Связанные данные из игры (${readonlyFields.length})`
      : `Что использует этот раздел (${readonlyFields.length})`;
    details.appendChild(summary);

    const inner = document.createElement("div");
    inner.className = "schema-sections top-gap";
    appendFieldSections(inner, readonlyFields);
    details.appendChild(inner);

    host.appendChild(details);
  }

  renderSchemaFilterMeta();
}

function queueListEdit(edit) {
  state.modding.currentListEdits.push(edit);
  renderCurrentListOps();
}

function renderCurrentListOps() {
  const meta = el("listOpsMeta");
  const host = el("listOpsQueue");
  host.innerHTML = "";
  const listEdits = state.modding.currentListEdits;

  if (!listEdits.length) {
    meta.textContent = "Изменений состава пока нет.";
    return;
  }

  const previewHint = schemaActionableTargets(state.modding.currentSchema).length > 0
    ? " После добавления нажми «Показать результат и открыть новые настройки»."
    : "";
  meta.textContent = `Подготовлено действий: ${listEdits.length}.${previewHint}`;
  listEdits.forEach((op, index) => {
    const item = document.createElement("div");
    item.className = "list-op-item";
    const text = document.createElement("span");
    text.textContent = `${index + 1}. ${formatListAction(op)}: ${op.targetLabel || op.targetPath}`;

    const remove = document.createElement("button");
    remove.type = "button";
    remove.textContent = "Убрать";
    remove.addEventListener("click", () => {
      state.modding.currentListEdits.splice(index, 1);
      renderCurrentListOps();
    });

    item.append(text, remove);
    host.appendChild(item);
  });
}

function renderListTargets() {
  const host = el("listTargetRows");
  host.innerHTML = "";

  const schema = state.modding.currentSchema;
  const listTargets = getFilteredListTargets(schema);
  const filterTerm = getSchemaFilterTerm();
  if (!listTargets.length) {
    const empty = document.createElement("div");
    empty.className = "muted";
    empty.textContent = filterTerm
      ? "По этому слову среди состава и связей ничего не найдено."
      : "В этом разделе нет списков, которые можно безопасно расширять или сокращать.";
    host.appendChild(empty);
    renderCurrentListOps();
    renderSchemaFilterMeta();
    return;
  }

  listTargets.forEach((target, index) => {
    const card = document.createElement("details");
    card.className = "list-target-card";
    const isAddableTarget = target.supportsAddReference || target.supportsAddClone || target.supportsAddEmpty;
    card.open = (listTargets.length === 1 && index === 0)
      || (index === 0 && isAddableTarget)
      || (((schema?.fields?.length || 0) === 0) && index === 0 && isAddableTarget);

    const title = document.createElement("summary");
    title.className = "list-target-summary";
    title.textContent = `${target.label} (${target.itemCount})`;

    const descr = document.createElement("div");
    descr.className = "list-target-description small muted";
    descr.textContent = `${target.description} Сейчас элементов: ${target.itemCount}.`;

    const actions = document.createElement("div");
    actions.className = "list-actions";

    const entryLabels = Array.isArray(target.entryLabels) ? target.entryLabels.filter((value) => String(value || "").trim()) : [];
    let currentEntries = null;
    if (entryLabels.length) {
      currentEntries = document.createElement("div");
      currentEntries.className = "list-entry-list";

      const caption = document.createElement("div");
      caption.className = "small muted";
      caption.textContent = "Текущий состав:";
      currentEntries.appendChild(caption);

      entryLabels.forEach((entryLabel, entryIndex) => {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "list-entry-chip";
        btn.textContent = `Убрать: ${entryLabel}`;
        btn.addEventListener("click", () => {
          queueListEdit({
            targetPath: target.targetPath,
            targetLabel: target.label,
            action: "remove-index",
            index: entryIndex,
            sourceIndex: null,
            templateJson: null
          });
        });
        currentEntries.appendChild(btn);
      });
    }

    if (target.supportsAddClone) {
      const srcInput = document.createElement("input");
      srcInput.type = "number";
      srcInput.min = "0";
      srcInput.value = String(Math.max(0, target.itemCount - 1));
      srcInput.className = "small-input";

      const btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = "Добавить ещё один такой же";
      btn.addEventListener("click", () => {
        queueListEdit({
          targetPath: target.targetPath,
          targetLabel: target.label,
          action: "add-clone",
          index: null,
          sourceIndex: Math.max(0, toIntSafe(srcInput.value, 0)),
          templateJson: null
        });
      });

      actions.append(srcInput, btn);
    }

    if (target.supportsAddReference && target.referencePickerKind) {
      const pickerWrap = document.createElement("div");
      pickerWrap.className = "list-reference-picker";
      let sourceSelect = null;

      if (target.itemKind === "reference-map" && entryLabels.length > 0) {
        const sourceWrap = document.createElement("label");
        sourceWrap.className = "field-with-help";

        const sourceCaption = document.createElement("div");
        sourceCaption.className = "small muted";
        sourceCaption.textContent = "Новая запись возьмёт правила из:";

        sourceSelect = document.createElement("select");
        sourceSelect.className = "field-input";
        entryLabels.forEach((entryLabel, entryIndex) => {
          const option = document.createElement("option");
          option.value = String(entryIndex);
          option.textContent = entryLabel;
          sourceSelect.appendChild(option);
        });
        sourceSelect.value = String(Math.max(0, target.itemCount - 1));

        sourceWrap.append(sourceCaption, sourceSelect);
        pickerWrap.appendChild(sourceWrap);
      }

      const getReferenceSourceIndex = () => sourceSelect
        ? Math.max(0, toIntSafe(sourceSelect.value, Math.max(0, target.itemCount - 1)))
        : Math.max(0, target.itemCount - 1);

       const quickHints = getQuickPickerHints(target.referencePickerKind);
        if (quickHints.length) {
          const quickRow = document.createElement("div");
          quickRow.className = "quick-action-row";
         quickHints.forEach((hint) => {
           const chip = document.createElement("button");
           chip.type = "button";
           chip.className = "quick-action-chip";
           chip.textContent = hint.label;
           chip.addEventListener("click", () => {
             search.value = hint.term;
             refreshReferenceResults();
           });
           quickRow.appendChild(chip);
         });
         pickerWrap.appendChild(quickRow);
       }

      const search = document.createElement("input");
      search.type = "text";
      search.className = "field-input";
      search.placeholder = buildReferenceSearchPlaceholder(target.referencePickerKind, target.referencePickerPrompt || "Введи хотя бы 2 буквы для поиска");

      const pickerToolbar = document.createElement("div");
      pickerToolbar.className = "picker-toolbar";

      const showOptionsBtn = document.createElement("button");
      showOptionsBtn.type = "button";
      showOptionsBtn.textContent = "Показать варианты";
      showOptionsBtn.addEventListener("click", () => {
        refreshReferenceResults();
        search.focus();
      });

      const results = document.createElement("div");
      results.className = "picker-results";

      let requestToken = 0;
      async function refreshReferenceResults() {
        const myToken = ++requestToken;
        results.innerHTML = "";
        const term = search.value.trim();
        if (!term) {
          try {
            const options = await fetchReferenceOptions(target.referencePickerKind, "", 8);
            if (myToken !== requestToken) {
              return;
            }

            const info = document.createElement("div");
            info.className = "small muted";
            info.textContent = buildPickerIntroText(target.referencePickerKind, quickHints.length > 0, true);
            results.appendChild(info);

            for (const option of Array.isArray(options) ? options : []) {
              const btn = document.createElement("button");
              btn.type = "button";
              btn.className = "picker-result";
              btn.textContent = `${buildReferenceActionLabel(target.referencePickerKind)}: ${option.label}`;
              btn.addEventListener("click", () => {
                queueListEdit({
                  targetPath: target.targetPath,
                  targetLabel: target.label,
                  action: "add-reference",
                  index: null,
                  sourceIndex: getReferenceSourceIndex(),
                  templateJson: null,
                  rawValue: option.value,
                  rawLabel: option.label
                });
                search.value = "";
                results.innerHTML = "";
              });
              results.appendChild(btn);
            }
          } catch (error) {
            if (myToken !== requestToken) {
              return;
            }

            const fail = document.createElement("div");
            fail.className = "small muted";
            fail.textContent = error.message || "Не удалось загрузить список ссылок.";
            results.appendChild(fail);
          }
          return;
        }

        try {
          const options = await fetchReferenceOptions(target.referencePickerKind, term, 10);
          if (myToken !== requestToken) {
            return;
          }

          const rows = Array.isArray(options) ? options : [];
          if (!rows.length) {
            const empty = document.createElement("div");
            empty.className = "small muted";
            empty.textContent = buildPickerIntroText(target.referencePickerKind, quickHints.length > 0, false);
            results.appendChild(empty);
            return;
          }

          for (const option of rows) {
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "picker-result";
            btn.textContent = `${buildReferenceActionLabel(target.referencePickerKind)}: ${option.label}`;
            btn.addEventListener("click", () => {
              queueListEdit({
                targetPath: target.targetPath,
                targetLabel: target.label,
                action: "add-reference",
                index: null,
                sourceIndex: getReferenceSourceIndex(),
                templateJson: null,
                rawValue: option.value,
                rawLabel: option.label
              });
              search.value = "";
              results.innerHTML = "";
            });
            results.appendChild(btn);
          }
        } catch (error) {
          if (myToken !== requestToken) {
            return;
          }

          const fail = document.createElement("div");
          fail.className = "small muted";
          fail.textContent = error.message || "Не удалось загрузить список ссылок.";
          results.appendChild(fail);
        }
      }

      search.addEventListener("input", () => {
        refreshReferenceResults();
      });
      search.addEventListener("focus", () => {
        if (!results.childElementCount) {
          refreshReferenceResults();
        }
      });

      pickerToolbar.append(search, showOptionsBtn);
      pickerWrap.append(pickerToolbar, results);
      actions.appendChild(pickerWrap);
    }

    if (target.supportsAddEmpty) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = (target.label || "").toLowerCase().includes("точки кривой")
        ? "Добавить точку"
        : "Добавить новый пустой";
      btn.addEventListener("click", () => {
        queueListEdit({
          targetPath: target.targetPath,
          targetLabel: target.label,
          action: "add-empty",
          index: null,
          sourceIndex: null,
          templateJson: "{}"
        });
      });
      actions.appendChild(btn);
    }

    if (target.supportsRemove && !entryLabels.length) {
      const idxInput = document.createElement("input");
      idxInput.type = "number";
      idxInput.min = "0";
      idxInput.value = String(Math.max(0, target.itemCount - 1));
      idxInput.className = "small-input";

      const btn = document.createElement("button");
      btn.type = "button";
      btn.textContent = (target.label || "").toLowerCase().includes("точки кривой")
        ? "Убрать точку №"
        : "Убрать элемент №";
      btn.addEventListener("click", () => {
        queueListEdit({
          targetPath: target.targetPath,
          targetLabel: target.label,
          action: "remove-index",
          index: Math.max(0, toIntSafe(idxInput.value, 0)),
          sourceIndex: null,
          templateJson: null
        });
      });

      actions.append(idxInput, btn);
    }

    if (target.supportsClear) {
      const clearBtn = document.createElement("button");
      clearBtn.type = "button";
      clearBtn.textContent = "Очистить весь состав";
      clearBtn.addEventListener("click", () => {
        queueListEdit({
          targetPath: target.targetPath,
          targetLabel: target.label,
          action: "clear",
          index: null,
          sourceIndex: null,
          templateJson: null
        });
      });
      actions.appendChild(clearBtn);
    }

    const body = document.createElement("div");
    body.className = "list-target-body";
    body.append(descr);
    if (currentEntries) {
      body.appendChild(currentEntries);
    }
    body.appendChild(actions);

    card.append(title, body);
    host.appendChild(card);
  });

  renderCurrentListOps();
  renderSchemaFilterMeta();
}

async function loadSelectedAssetSchema() {
  const assetId = state.modding.selectedAssetId;
  if (!assetId) {
    clearSchemaView();
    return;
  }

  const loadToken = ++state.modding.schemaLoadToken;
  const selected = selectedAssetFromCurrentPage();
  state.modding.selectedAsset = selected;
  if (selected) {
    el("schemaAssetTitle").textContent = selected.displayName || "Выбранный раздел";
    el("schemaAssetSummary").textContent = selected.summary || "";
  }
  renderSelectedAssetPreview();

  setSchemaMeta("Загрузка параметров...");
  clearScenePanelContent();
  el("schemaWarnings").innerHTML = "";
  el("schemaSections").innerHTML = '<div class="schema-loading muted">Читаю безопасные настройки из игры. На больших ассетах это может занять несколько секунд.</div>';
  el("listTargetRows").innerHTML = '<div class="schema-loading muted">Собираю состав системы и связанные элементы...</div>';
  const schema = await api(`/api/modding/schema?assetId=${encodeURIComponent(assetId)}`);
  if (loadToken !== state.modding.schemaLoadToken || state.modding.selectedAssetId !== assetId) {
    return;
  }

  state.modding.currentSchema = schema;
  state.modding.currentFieldValues = new Map();
  state.modding.currentFieldDisplayValues = new Map();
  state.modding.currentOriginalValues = new Map();
  state.modding.currentListEdits = [];
  state.modding.schemaFieldFilter = "";
  state.modding.currentSceneFilterKind = "all";
  state.modding.currentSceneSearch = "";
  state.modding.currentSceneFocusMode = "all";
  if (el("schemaFieldFilter")) {
    el("schemaFieldFilter").value = "";
  }
  if (el("sceneTypeFilter")) {
    el("sceneTypeFilter").value = "all";
  }
  if (el("sceneSearchInput")) {
    el("sceneSearchInput").value = "";
  }
  if (el("sceneFocusMode")) {
    el("sceneFocusMode").value = "all";
  }

  for (const field of schema.fields || []) {
    state.modding.currentFieldValues.set(field.fieldPath, field.currentValue);
    state.modding.currentFieldDisplayValues.set(field.fieldPath, field.currentDisplayValue || referenceValueToReadableName(field.currentValue));
    state.modding.currentOriginalValues.set(field.fieldPath, field.currentValue);
  }
  seedModelFitControlsFromSchema();

  renderSchemaWarnings(schema.warnings || []);
  setSchemaMeta(describeSchemaMeta(schema));
  renderSchemaFilterMeta();

  renderSceneEditor();
  renderSchemaFields();
  renderListTargets();
  renderSelectedAssetPreview();
  refreshModelReplacementModels().catch(showError);
  loadVehicleProfileForCurrentAsset().catch(showError);
}

function renderStagedEdits() {
  const host = el("stagedList");
  host.innerHTML = "";
  renderStudioFlowBar();

  const staged = Array.from(state.modding.stagedByAssetId.values());
  renderBuildReadiness();
  if (!staged.length) {
    el("stagedMeta").textContent = "В мод пока ничего не сохранено.";
    renderSelectedAssetPreview();
    return;
  }

  el("stagedMeta").textContent =
    `Систем в моде: ${staged.length}. Все изменения войдут в один общий файл .pak.`;

  for (const item of staged) {
    const card = document.createElement("div");
    card.className = "staged-card";

    const left = document.createElement("div");
    const title = document.createElement("strong");
    title.textContent = item.displayName || item.relativePath;
    const info = document.createElement("div");
    info.className = "small muted";
    info.textContent = describeStagedItem(item);
    left.append(title, info);

    const removeBtn = document.createElement("button");
    removeBtn.type = "button";
    removeBtn.textContent = "Убрать";
    removeBtn.addEventListener("click", () => {
      state.modding.stagedByAssetId.delete(item.assetId);
      renderStagedEdits();
      updateModAssetMeta();
    });

    card.append(left, removeBtn);
    host.appendChild(card);
  }

  renderSelectedAssetPreview();
}

function renderBuildReadiness() {
  const host = el("buildReadiness");
  if (!host) {
    return;
  }

  const stagedCount = state.modding.stagedByAssetId.size;
  host.innerHTML = "";
  host.classList.toggle("ready", stagedCount > 0);

  const title = document.createElement("div");
  title.className = "build-readiness-title";
  title.textContent = stagedCount > 0
    ? "Мод готов к сборке"
    : "Добавь хотя бы одно изменение";
  host.appendChild(title);

  const note = document.createElement("div");
  note.className = "build-readiness-note";
  const installText = el("installCheck")?.checked ? "установка включена" : "только файл";
  const zipText = el("zipCheck")?.checked ? "zip включён" : "без zip";
  note.textContent = stagedCount > 0
    ? `${stagedCount} разделов в очереди | ${installText} | ${zipText}`
    : "Выбери систему, измени безопасные поля или приготовь модель, затем добавь результат в мод.";
  host.appendChild(note);
}

function stageCurrentAssetEdits() {
  const schema = state.modding.currentSchema;
  if (!schema) {
    throw new Error("Сначала выбери раздел и открой его настройки.");
  }

  const changedFields = [];
  for (const field of schema.fields || []) {
    const original = state.modding.currentOriginalValues.get(field.fieldPath) ?? "";
    const current = state.modding.currentFieldValues.get(field.fieldPath) ?? "";
    if (String(original) === String(current)) {
      continue;
    }

    changedFields.push({
      fieldPath: field.fieldPath,
      value: String(current)
    });
  }

  const listEdits = state.modding.currentListEdits.map((x) => ({ ...x }));
  const existing = state.modding.stagedByAssetId.get(schema.assetId) || null;
  if (!changedFields.length && !listEdits.length) {
    if (schema.sourceKind === "preview" && existing) {
      renderStagedEdits();
      updateModAssetMeta();
      return existing;
    }

    state.modding.stagedByAssetId.delete(schema.assetId);
    renderStagedEdits();
    updateModAssetMeta();
    return null;
  }

  const selected = state.modding.selectedAsset || selectedAssetFromCurrentPage();
  const nextFields = schema.sourceKind === "preview" && existing
    ? mergeFieldEdits(existing.edits, changedFields)
    : changedFields;
  const nextListEdits = schema.sourceKind === "preview" && existing
    ? [...existing.listEdits, ...listEdits]
    : listEdits;

  const stagedItem = {
    assetId: schema.assetId,
    relativePath: schema.relativePath,
    displayName: selected?.displayName || schema.relativePath,
    sourceMode: el("schemaSourceMode").value,
    companionMode: el("schemaCompanionMode").value,
    edits: nextFields,
    listEdits: nextListEdits
  };

  state.modding.stagedByAssetId.set(schema.assetId, stagedItem);

  renderStagedEdits();
  updateModAssetMeta();
  return stagedItem;
}

function mergeFieldEdits(existing, incoming) {
  const merged = new Map();
  for (const field of Array.isArray(existing) ? existing : []) {
    merged.set(field.fieldPath, { ...field });
  }

  for (const field of Array.isArray(incoming) ? incoming : []) {
    merged.set(field.fieldPath, { ...field });
  }

  return Array.from(merged.values());
}

async function previewStagedAssetEdits(stagedItem) {
  if (!stagedItem?.assetId) {
    throw new Error("Сначала выбери раздел и подготовь изменения.");
  }

  clearScenePanelContent();
  const schema = await api("/api/modding/schema-preview", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      assetId: stagedItem.assetId,
      edits: stagedItem.edits,
      listEdits: stagedItem.listEdits,
      sourceMode: stagedItem.sourceMode === "auto" ? null : stagedItem.sourceMode,
      companionMode: stagedItem.companionMode === "auto" ? null : stagedItem.companionMode
    })
  });

  state.modding.currentSchema = schema;
  state.modding.currentFieldValues = new Map();
  state.modding.currentFieldDisplayValues = new Map();
  state.modding.currentOriginalValues = new Map();
  state.modding.currentListEdits = [];

  for (const field of schema.fields || []) {
    state.modding.currentFieldValues.set(field.fieldPath, field.currentValue);
    state.modding.currentFieldDisplayValues.set(field.fieldPath, field.currentDisplayValue || referenceValueToReadableName(field.currentValue));
    state.modding.currentOriginalValues.set(field.fieldPath, field.currentValue);
  }
  seedModelFitControlsFromSchema();

  renderSchemaWarnings(schema.warnings || []);
  setSchemaMeta(describeSchemaMeta(schema));
  renderSchemaFilterMeta();
  renderSceneEditor();
  renderSchemaFields();
  renderListTargets();
  renderSelectedAssetPreview();
  refreshModelReplacementModels().catch(showError);
  loadVehicleProfileForCurrentAsset().catch(showError);
}

function logBuild(text) {
  const out = el("buildOutput");
  out.textContent += `${text}\n`;
  out.scrollTop = out.scrollHeight;
}

async function buildMod() {
  const staged = Array.from(state.modding.stagedByAssetId.values());
  if (!staged.length) {
    throw new Error("Нет изменений для сборки. Сначала добавь в мод хотя бы один раздел.");
  }

  const payload = {
    modName: el("modNameInput").value.trim(),
    installToGame: el("installCheck").checked,
    createZip: el("zipCheck").checked,
    seedCompanions: el("seedCheck").checked,
    enabledPresetIds: [],
    enabledFeatureIds: [],
    featureSettings: [],
    selectedAssetIds: staged.map((x) => x.assetId),
    assetSettings: staged.map((x) => ({
      assetId: x.assetId,
      enabled: true,
      sourceMode: x.sourceMode === "auto" ? null : x.sourceMode,
      companionMode: x.companionMode === "auto" ? null : x.companionMode
    })),
    assetEdits: staged.map((x) => ({
      assetId: x.assetId,
      edits: x.edits,
      listEdits: x.listEdits.map((op) => ({
        targetPath: op.targetPath,
        action: op.action,
        index: op.index,
        sourceIndex: op.sourceIndex,
        templateJson: op.templateJson,
        rawValue: op.rawValue || null
      }))
    })),
    recipes: []
  };

  el("buildOutput").textContent = "";
  logBuild("Запуск сборки...");
  logBuild(`Изменённых разделов: ${staged.length}`);

  const result = await api("/api/build", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  if (!result.ok) {
    logBuild(`Ошибка: ${result.error || "неизвестная ошибка"}`);
    return;
  }

  logBuild("Готово.");
  logBuild(`Файл PAK: ${result.outputPakPath}`);
  if (result.outputZipPath) {
    logBuild(`Архив ZIP: ${result.outputZipPath}`);
  }
  if (result.installedPakPath) {
    logBuild(`Установлено в игру: ${result.installedPakPath}`);
  }
  logBuild(`Файлов: ${result.presetFileCount} | Связанных файлов: ${result.seededCompanionCount} | Переопределений: ${result.overrideCount}`);
  if (result.warnings?.length) {
    logBuild("Предупреждения:");
    for (const warning of result.warnings) {
      logBuild(`- ${warning}`);
    }
  }
}

function showError(err) {
  const message = err instanceof Error ? err.message : String(err);
  const out = el("buildOutput");
  if (out) {
    out.textContent += `Ошибка: ${message}\n`;
  }
  alert(`Ошибка: ${message}`);
}

function setupActions() {
  el("supportAuthorBtn").addEventListener("click", openSupportModal);
  el("supportCloseBtn").addEventListener("click", closeSupportModal);
  el("supportModal").addEventListener("click", (event) => {
    if (event.target === el("supportModal")) {
      closeSupportModal();
    }
  });
  el("supportCopyCardBtn").addEventListener("click", () => copySupportCard().catch(showError));
  el("supportOpenSberBtn").addEventListener("click", openSberOnline);
  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      closeSupportModal();
    }
  });

  el("updateCheckBtn").addEventListener("click", async () => {
    try {
      const result = await runAppUpdateAction("/api/app-update/check");
      const updateStatus = state.appUpdate.status;
      if (result.message && !updateStatus?.updateAvailable && !updateStatus?.pendingRestart) {
        alert(result.message);
      }
    } catch (err) {
      showError(err);
    }
  });

  el("updateDownloadBtn").addEventListener("click", async () => {
    try {
      await runAppUpdateAction("/api/app-update/download");
    } catch (err) {
      showError(err);
    }
  });

  el("updateInstallBtn").addEventListener("click", async () => {
    try {
      const confirmed = window.confirm("Программа закроется, установит новую версию и запустится снова. Продолжить?");
      if (!confirmed) {
        return;
      }

      await runAppUpdateAction("/api/app-update/install");
    } catch (err) {
      showError(err);
    }
  });

  el("modAssetSearch").addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      state.modding.page = 1;
      loadModdingAssets().catch(showError);
    }
  });

  el("modAssetSearch").addEventListener("input", () => {
    window.clearTimeout(modAssetSearchDebounce);
    modAssetSearchDebounce = window.setTimeout(() => {
      state.modding.page = 1;
      loadModdingAssets().catch(showError);
    }, 260);
  });

  el("modCategorySelect").addEventListener("change", () => {
    state.modding.selectedCategoryId = el("modCategorySelect").value;
    state.modding.page = 1;
    renderCategoryChips();
    loadModdingAssets().catch(showError);
  });

  el("modPageSize").addEventListener("change", () => {
    state.modding.page = 1;
    loadModdingAssets().catch(showError);
  });

  el("modOnlyEditableCheck").addEventListener("change", () => {
    const visibleAssets = syncSelectedAssetWithVisibleList();
    renderModAssetRows();
    if (!visibleAssets.length) {
      clearSchemaView();
      return;
    }

    syncSchemaAfterAssetListChange();
  });
  ["installCheck", "zipCheck", "seedCheck"].forEach((id) => {
    const input = el(id);
    if (!input) {
      return;
    }
    input.addEventListener("change", renderBuildReadiness);
  });

  el("modPrevBtn").addEventListener("click", () => {
    if (state.modding.page <= 1) {
      return;
    }
    state.modding.page -= 1;
    loadModdingAssets().catch(showError);
  });

  el("modNextBtn").addEventListener("click", () => {
    if (state.modding.page >= modPageCount()) {
      return;
    }
    state.modding.page += 1;
    loadModdingAssets().catch(showError);
  });

  el("loadSchemaBtn").addEventListener("click", () => loadSelectedAssetSchema().catch(showError));
  el("customVisualImportBtn").addEventListener("click", () => importCustomVisualAssets().catch(showError));
  el("modelReplacementField").addEventListener("change", () => {
    state.modding.modelProfilePreset = "auto";
    resetModelCookTouchedControls();
    refreshModelReplacementWizard();
  });
  document.querySelectorAll(".model-profile-btn").forEach((button) => {
    button.addEventListener("click", () => {
      resetModelCookTouchedControls();
      applyModelCookProfile(button.dataset.modelProfile || "auto");
      refreshModelReplacementWizard();
    });
  });
  el("modelTargetLongestCm").addEventListener("input", () => {
    state.modding.modelTargetLongestTouched = true;
    syncModelTargetLongestSlider("number");
    syncModelCookProfileControls(getSelectedModelReplacementField());
  });
  el("modelTargetLongestSlider").addEventListener("input", () => {
    state.modding.modelTargetLongestTouched = true;
    syncModelTargetLongestSlider("slider");
    syncModelCookProfileControls(getSelectedModelReplacementField());
  });
  [
    "modelTriangleBudget",
    "modelFitOffsetX",
    "modelFitOffsetY",
    "modelFitOffsetZ",
    "modelFitPitch",
    "modelFitYaw",
    "modelFitRoll",
    "weaponGripAnchorPercent",
    "weaponGripDiameterCm",
    "weaponGripBackReachCm",
    "weaponSecondHandShiftCm",
    "vehicleCollisionMode",
    "vehicleQueryProxyLength",
    "vehicleQueryProxyWidth",
    "vehicleQueryProxyHeight",
    "vehicleSeatOffsetX",
    "vehicleSeatOffsetY",
    "vehicleSeatOffsetZ",
    "vehiclePassengerSeatOffsetX",
    "vehiclePassengerSeatOffsetY",
    "vehiclePassengerSeatOffsetZ",
    "vehicleEntryOffsetX",
    "vehicleEntryOffsetY",
    "vehicleEntryOffsetZ"
  ].forEach((id) => {
    const input = el(id);
    if (!input) {
      return;
    }
    input.addEventListener("input", () => {
      input.dataset.userTouched = "1";
      syncModelCookProfileControls(getSelectedModelReplacementField());
    });
    input.addEventListener("change", () => {
      input.dataset.userTouched = "1";
      syncModelCookProfileControls(getSelectedModelReplacementField());
    });
  });
  el("modelMaterialMode").addEventListener("change", () => {
    if (el("modelMaterialMode").value === "custom" && getModelFitNumber("modelPaintStrength") <= 0) {
      setModelFitNumber("modelPaintStrength", 100);
    }
    syncModelMaterialControls();
    if (el("modelMaterialMode").value === "game") {
      refreshModelMaterialReferenceOptions().catch(showError);
    }
    syncModelCookProfileControls(getSelectedModelReplacementField());
    refreshModelReplacementWizard();
  });
  el("modelMaterialSearch").addEventListener("input", queueModelMaterialReferenceRefresh);
  el("modelReplacementRefreshBtn").addEventListener("click", () => refreshModelReplacementModels().catch(showError));
  el("rawModelCookSource").addEventListener("change", () => {
    renderRawModelAnalysisPanel();
    syncModelCookProfileControls(getSelectedModelReplacementField());
    loadArmorSetPlanForCurrentSelection().catch(showError);
    loadVehicleModulePlanForCurrentSelection().catch(showError);
  });
  el("rawModelCookBtn").addEventListener("click", () => cookSelectedRawModelForReplacement().catch(showError));
  el("vehicleFullReplacementBtn").addEventListener("click", () => cookVehicleFullReplacementForCurrentSelection().catch(showError));
  el("modelReplacementApplyBtn").addEventListener("click", () => {
    try {
      applyModelReplacement();
    } catch (error) {
      showError(error);
    }
  });
  el("schemaFieldFilter").addEventListener("input", () => {
    state.modding.schemaFieldFilter = el("schemaFieldFilter").value;
    renderSceneEditor();
    renderSchemaFields();
    renderListTargets();
    renderSchemaFilterMeta();
  });
  el("sceneTypeFilter").addEventListener("change", () => {
    state.modding.currentSceneFilterKind = el("sceneTypeFilter").value || "all";
    renderSceneEditor();
  });
  el("sceneSearchInput").addEventListener("input", () => {
    state.modding.currentSceneSearch = el("sceneSearchInput").value || "";
    renderSceneEditor();
  });
  el("sceneFocusMode").addEventListener("change", () => {
    state.modding.currentSceneFocusMode = el("sceneFocusMode").value || "all";
    renderSceneEditor();
  });
  el("stageAssetBtn").addEventListener("click", () => {
    try {
      stageCurrentAssetEdits();
    } catch (err) {
      showError(err);
    }
  });
  el("previewAssetBtn").addEventListener("click", async () => {
    try {
      const stagedItem = stageCurrentAssetEdits();
      if (!stagedItem) {
        throw new Error("Сначала измени что-нибудь в этом разделе.");
      }

      await previewStagedAssetEdits(stagedItem);
    } catch (err) {
      showError(err);
    }
  });

  el("clearStagedBtn").addEventListener("click", () => {
    state.modding.stagedByAssetId.clear();
    renderStagedEdits();
    updateModAssetMeta();
  });

  window.addEventListener("pointermove", handleScenePointerMove);
  window.addEventListener("pointerup", handleScenePointerUp);
  window.addEventListener("pointercancel", handleScenePointerUp);

  el("buildBtn").addEventListener("click", () => buildMod().catch(showError));
}

async function init() {
  setupActions();
  setDefaultModName();
  await loadStatus();
  await loadAppUpdateStatus(true);
  await loadModdingCategories();
  await loadModdingAssets();
  renderStagedEdits();

  if (!state.appUpdate.pollHandle) {
    state.appUpdate.pollHandle = window.setInterval(() => {
      loadAppUpdateStatus(true);
    }, 4000);
  }
}

init().catch(showError);
