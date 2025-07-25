using System;
using System.Reflection;
using Chetch.Messaging;
using XmppDotNet.Xmpp.PubSub;

namespace Chetch.Arduino;

public interface IMessageUpdatableObject
{
    byte ID { get; set; }

    String UID { get; }

    //bool CanAssignMessageValue(PropertyInfo propertyInfo, ArduinoMessage message);
    bool AssignMessageValue(PropertyInfo propertyInfo, Object propertyValue, ArduinoMessage message);
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class ArduinoMessageMap : Attribute
{
    static Dictionary<Type, Dictionary<MessageType, Dictionary<PropertyInfo, byte>>> map = new Dictionary<Type, Dictionary<MessageType, Dictionary<PropertyInfo, byte>>>();
    static Object mapLock = new object();

    public static UpdatedProperties AssignMessageValues(IMessageUpdatableObject obj, ArduinoMessage message)
    {
        var type = obj.GetType();
        if(!map.ContainsKey(type))
        {
            lock(mapLock)
            {
                map[type] = new Dictionary<MessageType, Dictionary<PropertyInfo, byte>>();
            }
        }
        if(!map[type].ContainsKey(message.Type))
        {
            var prop2index = GetArduinoMessageMapProperties(type, message.Type);
            
            lock(mapLock)
            {
                map[type][message.Type] = prop2index;
            }
        }
        
        var updatedProperties = new UpdatedProperties(obj, message);
        foreach(var kv in map[type][message.Type])
        {
            var prop2set = kv.Key;
            var argIdx = kv.Value;
            var val = message.Get(argIdx, prop2set.PropertyType);
            var oldVal = kv.Key.GetValue(obj);

            if(obj.AssignMessageValue(prop2set, val, message))
            {
                //kv.Key.SetValue(obj, val);
                updatedProperties.Properties.Add(prop2set);
            }
        }
        return updatedProperties;
    }

    public static Dictionary<PropertyInfo, byte> GetArduinoMessageMapProperties(Type type, MessageType messageType)
    {
        var prop2index = new Dictionary<PropertyInfo, byte>();

        var props = type.GetProperties().Where(
            prop => Attribute.IsDefined(prop, typeof(ArduinoMessageMap)));

        foreach (var prop in props) {
            var amms = prop.GetCustomAttributes(true);
            foreach (ArduinoMessageMap amm in amms)
            {
                if (amm.MessageType == messageType)
                {
                    prop2index[prop] = amm.ArgumentIndex;
                }
            }
        }

        return prop2index;
    }

    public static ArduinoMessage CreateMessageFor(Object obj, MessageType messageType, String? propName = null, Object? propVal = null)
    {
        var message = new ArduinoMessage(messageType);
        var prop2index = GetArduinoMessageMapProperties(obj.GetType(), message.Type);

        foreach (var kv in prop2index)
        {
            var prop2set = kv.Key;
            var argIdx = kv.Value;
            var val = kv.Key.GetValue(obj);
            if (propName != null && prop2set.Name == propName) val = propVal;
            if (val != null)
            {
                message.Add(val, argIdx);
            }
        }
        return message;
    }

    public class UpdatedProperties
    {
        public ArduinoMessage? Message { get; internal set; }
        public IMessageUpdatableObject? UpdatedObject { get; internal set; }

        public List<PropertyInfo> Properties { get; internal set; } = new List<PropertyInfo>();

        public int Count => Properties.Count;

        public UpdatedProperties() { }

        public UpdatedProperties(IMessageUpdatableObject obj, ArduinoMessage message)
        {
            UpdatedObject = obj;
            Message = message;
        }
    }

    public MessageType MessageType { get; set; }

    public byte ArgumentIndex { get; set; }

    public int TestProp { get; set; } = 3;

    public ArduinoMessageMap(MessageType type, byte argumentIndex)
    {
        MessageType = type;
        ArgumentIndex = argumentIndex;
    }
}
