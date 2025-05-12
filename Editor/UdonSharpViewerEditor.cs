#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UdonSharp;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VRC.SDKBase;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI;
using LayerMaskField = UnityEditor.UIElements.LayerMaskField;
using Object = UnityEngine.Object;
using UnsignedIntegerField = UnityEngine.UIElements.UnsignedIntegerField;
using UnsignedLongField = UnityEngine.UIElements.UnsignedLongField;

namespace Hactazia.UdonTools
{
    public class UdonSharpInspectorEditor : EditorWindow
    {
        private Vector2 _objectListScroll;
        private Vector2 _objectDetailScroll;
        private List<UdonSharpBehaviour> _udonBehaviours;
        private UdonSharpBehaviour _selectedBehaviour;

        private VisualElement _root;

        [MenuItem("Tools/UdonSharp Inspector")]
        public static void ShowWindow()
            => GetWindow<UdonSharpInspectorEditor>("UdonSharp Inspector");

        private void OnFocus()
            => RefreshUdonBehaviours();

        private void OnHierarchyChange()
        {
            RefreshUdonBehaviours();
            Repaint();
        }

        private void RefreshUdonBehaviours()
        {
            _udonBehaviours = new List<UdonSharpBehaviour>(FindObjectsOfType<UdonSharpBehaviour>());
            if (!_udonBehaviours.Contains(_selectedBehaviour))
                _selectedBehaviour = null;
            UpdateListBehaviours();
            UpdateSelectedBehaviour();
            UpdateVariables();
            Check(_selectedBehaviour);
        }

        private void ChangeSelectedBehaviour(int instanceID)
        {
            var behaviour = _udonBehaviours.Find(b => b.GetInstanceID() == instanceID);
            if (!behaviour) return;
            _selectedBehaviour = behaviour;
            RefreshUdonBehaviours();
        }


        private void OnGUI()
        {
            if (rootVisualElement.childCount == 0)
            {
                _root?.RemoveFromHierarchy();
                _root = Resources.Load<VisualTreeAsset>("UdonSharpInspectorEditor").CloneTree();
                rootVisualElement.Add(_root);
                rootVisualElement.style.flexShrink = 1;
                rootVisualElement.style.flexGrow = 1;
                _root.style.flexGrow = 1;
                _root.style.flexShrink = 1;

                var ping = _root?.Q<Button>("ping");
                ping?.RegisterCallback<ClickEvent>(e =>
                {
                    if (!_selectedBehaviour) return;
                    EditorGUIUtility.PingObject(_selectedBehaviour);
                    Selection.activeGameObject = _selectedBehaviour.gameObject;
                    // and open the inspector with Windows > General > Inspector
                    var inspector = GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow"));
                    inspector?.Show();
                });

                var to_sync = _root?.Q<VisualElement>("to_sync");
                to_sync?.Q<Button>("manual")?.RegisterCallback<ClickEvent>(e =>
                {
                    var component = _selectedBehaviour?.GetComponent<VRC.Udon.UdonBehaviour>();
                    if (!component) return;
                    component.SyncMethod = Networking.SyncType.Manual;
                    RefreshUdonBehaviours();
                });
                to_sync?.Q<Button>("continuous")?.RegisterCallback<ClickEvent>(e =>
                {
                    var component = _selectedBehaviour?.GetComponent<VRC.Udon.UdonBehaviour>();
                    if (!component) return;
                    component.SyncMethod = Networking.SyncType.Continuous;
                    RefreshUdonBehaviours();
                });

                var to_desync = _root?.Q<VisualElement>("to_desync");
                to_desync?.Q<Button>("fix")?.RegisterCallback<ClickEvent>(e =>
                {
                    var component = _selectedBehaviour?.GetComponent<VRC.Udon.UdonBehaviour>();
                    if (!component) return;
                    component.SyncMethod = Networking.SyncType.None;
                    RefreshUdonBehaviours();
                });

                var sync = _root?.Q<EnumField>("sync");
                sync?.RegisterValueChangedCallback(e =>
                {
                    var component = _selectedBehaviour?.GetComponent<VRC.Udon.UdonBehaviour>();
                    if (!component || e.newValue is not Networking.SyncType syncType) return;
                    component.SyncMethod = syncType;
                    RefreshUdonBehaviours();
                });


                RefreshUdonBehaviours();
            }
        }

        private string GetPath(Transform transform)
        {
            var path = "";
            if (!transform || !transform.gameObject) return path;
            var scene = transform.gameObject.scene.name;
            while (transform)
            {
                path = "/" + transform.name + path;
                transform = transform.parent;
            }

            return scene + path;
        }


        private void UpdateListBehaviours()
        {
            var element = Resources.Load<VisualTreeAsset>("UdonSharpInspectorEditorElement");
            var listElements = _root?.Q("list");
            if (!element || listElements == null) return;

            // get all ids of the behaviours
            var instanceIDs = new HashSet<int>();
            foreach (var behaviour in _udonBehaviours.ToArray())
                instanceIDs.Add(behaviour.GetInstanceID());

            // remove all behaviours who are not in the list
            foreach (var child in listElements.Children().ToArray())
                if (child.userData is not int id || !instanceIDs.Contains(id))
                    listElements.Remove(child);
                else instanceIDs.Remove(id);

            // add all behaviours who are not in the list
            foreach (var behaviour in _udonBehaviours.ToArray())
            {
                if (!instanceIDs.Contains(behaviour.GetInstanceID())) continue;
                var elementInstance = element.CloneTree();
                elementInstance.userData = behaviour.GetInstanceID();
                listElements.Add(elementInstance);
                var label = elementInstance.Q<Button>("button");
                label.RegisterCallback<ClickEvent>(e => ChangeSelectedBehaviour(behaviour.GetInstanceID()));
            }

            foreach (var child in listElements.Children().ToArray())
            {
                if (child.userData is not int id) continue;
                var behaviour = _udonBehaviours.Find(b => b.GetInstanceID() == id);
                if (!behaviour) continue;
                var btn = child.Q<Button>("button");
                var check = Check(behaviour);
                if (btn != null)
                {
                    btn.SetEnabled(behaviour != _selectedBehaviour);
                    btn.tooltip = GetPath(behaviour.transform);
                }

                var text = child.Q<Label>("text");
                if (text != null) text.text = behaviour.name;

                var warning = child.Q("warning");
                var error = child.Q("error");
                if (warning != null)
                    warning.style.display = check.HasFlag(CheckFags.Warning)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                if (error != null)
                    error.style.display = check.HasFlag(CheckFags.Error)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }
        }


        private void UpdateSelectedBehaviour()
        {
            var behaviour = _selectedBehaviour;
            var infos = _root?.Q("infos");
            var noBehaviour = _root?.Q("no_behaviour");
            if (infos == null || noBehaviour == null) return;

            var component = behaviour?.GetUdonBehaviour();
            if (!component)
            {
                infos.style.display = DisplayStyle.None;
                noBehaviour.style.display = DisplayStyle.Flex;
                return;
            }

            infos.style.display = DisplayStyle.Flex;
            noBehaviour.style.display = DisplayStyle.None;

            var behaviourField = infos.Q<ObjectField>("behaviour");
            behaviourField.value = behaviour;

            var componentField = infos.Q<ObjectField>("component");
            componentField.value = component;

            var sourceField = infos.Q<ObjectField>("source");
            sourceField.value = component.programSource;

            var syncField = infos.Q<EnumField>("sync");
            syncField.value = component.SyncMethod;
        }

        private void UpdateVariables()
        {
            var variables = _root?.Q("variables");
            var noVariables = _root?.Q("no_variable");
            if (variables == null || noVariables == null) return;

            var dump = _selectedBehaviour.GetUdonSharpBackingUdonBehaviour();
            var program = dump?.GetProgram();
            if (program == null)
            {
                variables.style.display = DisplayStyle.None;
                noVariables.style.display = DisplayStyle.Flex;
                return;
            }

            var symbols = program.SymbolTable.GetSymbols();
            if (symbols.Length == 0)
            {
                variables.style.display = DisplayStyle.None;
                noVariables.style.display = DisplayStyle.Flex;
                return;
            }

            var symbolNames = new HashSet<string>();
            foreach (var symbol in symbols)
                symbolNames.Add(symbol);

            variables.style.display = DisplayStyle.Flex;
            noVariables.style.display = DisplayStyle.None;

            foreach (var child in variables.Children().ToArray())
                if (child.userData is not string fieldName || !symbolNames.Contains(fieldName))
                    variables.Remove(child);
                else symbolNames.Remove(fieldName);

            foreach (var symbol in symbols)
            {
                if (!symbolNames.Contains(symbol)) continue;
                var address = program.SymbolTable.GetAddressFromSymbol(symbol);
                var type = program.Heap.GetHeapVariableType(address);
                var elementInstance = CreateField(type);
                elementInstance.userData = symbol;
                variables.Add(elementInstance);
            }

            foreach (var child in variables.Children().ToArray())
            {
                if (child.userData is not string symbol || !symbolNames.Contains(symbol)) continue;

                var address = program.SymbolTable.GetAddressFromSymbol(symbol);
                var value = program.Heap.GetHeapVariable(address);
                var type = program.Heap.GetHeapVariableType(address);

                UpdateField(child, symbol, value);
                var infos = new List<string> { $"Type: {type.FullName}" };
                child.tooltip = string.Join("\n", infos);
                child.SetEnabled(true);
            }
        }

        private void UpdateField(VisualElement field, string name, object value)
        {
            if (field is Label label)
            {
                label.text = $"{name}: {value}";
                return;
            }

            if (field is TextField textField)
            {
                textField.value = value?.ToString() ?? "";
                textField.label = name;
                textField.SetEnabled(true);
                return;
            }

            if (field is IntegerField intField)
            {
                intField.value = Convert.ToInt32(value);
                intField.label = name;
                intField.SetEnabled(true);
                return;
            }

            if (field is UnsignedIntegerField uintField)
            {
                uintField.value = Convert.ToUInt32(value);
                uintField.label = name;
                uintField.SetEnabled(true);
                return;
            }

            if (field is LongField longField)
            {
                longField.value = Convert.ToInt64(value);
                longField.label = name;
                longField.SetEnabled(true);
                return;
            }

            if (field is UnsignedLongField ulongField)
            {
                ulongField.value = Convert.ToUInt64(value);
                ulongField.label = name;
                ulongField.SetEnabled(true);
                return;
            }

            if (field is FloatField floatField)
            {
                floatField.value = Convert.ToSingle(value);
                floatField.label = name;
                floatField.SetEnabled(true);
                return;
            }

            if (field is DoubleField doubleField)
            {
                doubleField.value = Convert.ToDouble(value);
                doubleField.label = name;
                doubleField.SetEnabled(true);
                return;
            }

            if (field is Toggle toggle)
            {
                toggle.value = Convert.ToBoolean(value);
                toggle.label = name;
                toggle.SetEnabled(true);
                return;
            }

            if (field is ObjectField objectField)
            {
                objectField.value = (Object)value;
                objectField.label = name;
                objectField.SetEnabled(true);
                return;
            }

            if (field is EnumField enumField)
            {
                enumField.value = (Enum)value;
                enumField.label = name;
                enumField.SetEnabled(true);
                return;
            }

            if (field is Vector2Field vector2Field)
            {
                vector2Field.value = (Vector2)value;
                vector2Field.label = name;
                vector2Field.SetEnabled(true);
                return;
            }

            if (field is Vector3Field vector3Field)
            {
                vector3Field.value = (Vector3)value;
                vector3Field.label = name;
                vector3Field.SetEnabled(true);
                return;
            }

            if (field is Vector4Field vector4Field)
            {
                vector4Field.value = (Vector4)value;
                vector4Field.label = name;
                vector4Field.SetEnabled(true);
                return;
            }

            if (field is QuaternionField quaternionField)
            {
                quaternionField.value = (Quaternion)value;
                quaternionField.label = name;
                quaternionField.SetEnabled(true);
                return;
            }

            if (field is ColorField colorField)
            {
                colorField.value = (Color)value;
                colorField.label = name;
                colorField.SetEnabled(true);
                return;
            }

            if (field is BoundsField boundsField)
            {
                boundsField.value = (Bounds)value;
                boundsField.label = name;
                boundsField.SetEnabled(true);
                return;
            }

            if (field is RectField rectField)
            {
                rectField.value = (Rect)value;
                rectField.label = name;
                rectField.SetEnabled(true);
                return;
            }

            if (field is LayerMaskField layerMaskField)
            {
                layerMaskField.value = (LayerMask)value;
                layerMaskField.label = name;
                layerMaskField.SetEnabled(true);
                return;
            }

            if (field is ListView listView)
            {
                var l = (IList)value;
                listView.itemsSource = l;
                listView.headerTitle = $"{name}";
                listView.makeItem = () => CreateField(l.GetType().GetElementType());
                listView.bindItem = (e, i) =>
                {
                    var item = l[i];
                    UpdateField(e, $"{name}[{i}]", item);
                };
                listView.unbindItem = (e, i) =>
                {
                    var item = l[i];
                    UpdateField(e, $"{name}[{i}]", item);
                };
                listView.Rebuild();
                return;
            }

            field.SetEnabled(false);
        }

        private VisualElement CreateField(Type type)
        {
            if (type == typeof(string))
                return new TextField();
            if (type == typeof(int))
                return new IntegerField();
            if (type == typeof(uint))
                return new UnsignedIntegerField();
            if (type == typeof(long))
                return new LongField();
            if (type == typeof(ulong))
                return new UnsignedLongField();
            if (type == typeof(float))
                return new FloatField();
            if (type == typeof(double))
                return new DoubleField();
            if (type == typeof(bool))
                return new Toggle();
            if (type == typeof(byte))
                return new IntegerField();
            if (type == typeof(sbyte))
                return new IntegerField();
            if (type == typeof(short))
                return new IntegerField();
            if (type == typeof(ushort))
                return new UnsignedIntegerField();
            if (type == typeof(char))
                return new IntegerField();
            if (type == typeof(decimal))
                return new FloatField();

            if (type == typeof(Vector2))
                return new Vector2Field();
            if (type == typeof(Vector3))
                return new Vector3Field();
            if (type == typeof(Vector4))
                return new Vector4Field();
            if (type == typeof(Quaternion))
                return new QuaternionField();
            if (type == typeof(Color))
                return new ColorField();
            if (type == typeof(Bounds))
                return new BoundsField();
            if (type == typeof(Rect))
                return new RectField();
            if (type == typeof(LayerMask))
                return new LayerMaskField();

            if (type.IsEnum)
                return new EnumField();

            if (type.IsArray)
                return new ListView
                {
                    style =
                    {
                        flexGrow = 1,
                        flexShrink = 1
                    },
                    headerTitle = "<array>",
                    showAddRemoveFooter = true,
                    showFoldoutHeader = true,
                    showBorder = true,
                    fixedItemHeight = 20,
                };

            if (type.IsSubclassOf(typeof(Object)))
                return new ObjectField();

            return new Label($"<text>: <text>")
            {
                // margin: 1px 3px 1px 3px
                style =
                {
                    marginLeft = 3,
                    marginRight = 3,
                    marginTop = 3,
                    marginBottom = 3
                }
            };
        }

        private FieldInfo[] GetFieldInfos(UdonSharpBehaviour behaviour)
            => behaviour.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private CheckFags Check(UdonSharpBehaviour behaviour)
        {
            var result = CheckFags.None;
            var component = behaviour?.GetUdonBehaviour();
            if (!component) return result;

            var isSynced = component.SyncMethod is not Networking.SyncType.None
                           && component.SyncMethod is not Networking.SyncType.Unknown;

            var fields = GetFieldInfos(behaviour);
            var syncAttr = fields
                .SelectMany(f => f.GetCustomAttributes(typeof(UdonSyncedAttribute), true)
                    as UdonSyncedAttribute[])
                .ToArray();
            var isSyncedAttr = syncAttr.Any(e => e.NetworkSyncType is not UdonSyncMode.NotSynced);

            var toSyncWarn = !isSynced && isSyncedAttr;
            result |= toSyncWarn ? CheckFags.Warning : CheckFags.None;
            var toDeSyncWarn = isSynced && !isSyncedAttr;
            result |= toDeSyncWarn ? CheckFags.Warning : CheckFags.None;

            if (behaviour != _selectedBehaviour) return result;

            var toSync = _root?.Q<VisualElement>("to_sync");
            if (toSync != null)
            {
                toSync.style.display = toSyncWarn ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var toDeSync = _root?.Q<VisualElement>("to_desync");
            if (toDeSync != null)
            {
                toDeSync.style.display = toDeSyncWarn ? DisplayStyle.Flex : DisplayStyle.None;
            }

            return result;
        }

        private string ParseAttribute(object a)
        {
            var s = (a.GetType().FullName ?? a.GetType().Name).Replace("Attribute", "");
            return a switch
            {
                UdonSyncedAttribute synced => $"{s}[SyncType={synced.NetworkSyncType.ToString()}]",
                _ => s
            };
        }
    }

    [Flags]
    public enum CheckFags
    {
        None = 0,
        Warning = 1,
        Error = 2,
    }
}
#endif