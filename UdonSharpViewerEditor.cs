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
using VRC.Udon.Common.Interfaces;

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

            var component = behaviour?.GetComponent<VRC.Udon.UdonBehaviour>();
            if (!behaviour || !component)
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

        private FieldInfo[] GetFieldInfos(UdonSharpBehaviour behaviour)
        {
            if (!behaviour) return Array.Empty<FieldInfo>();
            var serializedObject = new SerializedObject(behaviour);
            var fieldProp = serializedObject.GetIterator();

            var fieldNames = new List<string>();
            while (fieldProp.NextVisible(true))
                fieldNames.Add(fieldProp.name);

            return behaviour.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => fieldNames.Contains(field.Name))
                .ToArray();
        }

        private void UpdateVariables()
        {
            var variables = _root?.Q("variables");
            var noVariables = _root?.Q("no_variable");
            var type = _selectedBehaviour?.GetType();
            if (variables == null || noVariables == null || type == null) return;

            var fields = GetFieldInfos(_selectedBehaviour);
            if (fields.Length == 0)
            {
                variables.style.display = DisplayStyle.None;
                noVariables.style.display = DisplayStyle.Flex;
                return;
            }

            var fieldNames = new HashSet<string>();
            foreach (var field in fields)
                fieldNames.Add(field.Name);

            variables.style.display = DisplayStyle.Flex;
            noVariables.style.display = DisplayStyle.None;

            foreach (var child in variables.Children().ToArray())
                if (child.userData is not string fieldName || !fieldNames.Contains(fieldName))
                    variables.Remove(child);
                else fieldNames.Remove(fieldName);


            // var element = Resources.Load<VisualTreeAsset>("UdonSharpInspectorEditorVariable");
            foreach (var field in fields)
            {
                if (!fieldNames.Contains(field.Name)) continue;
                var elementInstance = new PropertyField();
                elementInstance.userData = field.Name;
                variables.Add(elementInstance);
            }

            foreach (var child in variables.Children().ToArray())
            {
                if (child.userData is not string fieldName) continue;
                var field = fields.FirstOrDefault(f => f.Name == fieldName);
                if (field == null || child is not PropertyField elementInstance) continue;
                var fp = _selectedBehaviour.GetType().GetField(field.Name);
                if (fp != null)
                {
                    // get if have [UdonSynced] attribute
                    elementInstance.BindProperty(new SerializedObject(_selectedBehaviour).FindProperty(field.Name));
                    elementInstance.label = ObjectNames.NicifyVariableName(field.Name);

                    var infos = new List<string>
                    {
                        $"Name: {type.FullName}.{field.Name}",
                        $"Type: {field.FieldType.FullName}",
                        "Attributes: "
                    };

                    var attrs = field.GetCustomAttributes(true)
                        .Select(a => a)
                        .Select(a => $" - {ParseAttribute(a)}")
                        .ToArray();
                    infos.Add(attrs.Length > 0
                        ? string.Join(", ", attrs)
                        : " - No attributes");
                    infos.Add("Flags: " + field.Attributes);

                    elementInstance.tooltip = string.Join("\n", infos);
                    elementInstance.SetEnabled(true);
                }
                else
                {
                    elementInstance.label = field.Name;
                    elementInstance.tooltip = "Field not found";
                    elementInstance.SetEnabled(false);
                }
            }
        }

        private CheckFags Check(UdonSharpBehaviour behaviour)
        {
            var result = CheckFags.None;
            var component = behaviour?.GetComponent<VRC.Udon.UdonBehaviour>();
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