using System;
using System.Reflection;
using Chetch.Messaging;

namespace Chetch.Arduino;

public interface IMessageUpdatableObject
{
    byte ID { get; set; }

    String Name { get;  }
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class ArduinoMessageMap : Attribute
{
    static Dictionary<Type, Dictionary<MessageType, Dictionary<PropertyInfo, byte>>> map = new Dictionary<Type, Dictionary<MessageType, Dictionary<PropertyInfo, byte>>>();
    
    public static UpdatedProperties AssignMessageValues(IMessageUpdatableObject obj, ArduinoMessage message)
    {
        var type = obj.GetType();
        if(!map.ContainsKey(type))
        {
            map[type] = new Dictionary<MessageType, Dictionary<PropertyInfo, byte>>();
        }
        if(!map[type].ContainsKey(message.Type))
        {
            var prop2index = new Dictionary<PropertyInfo, byte>();

            var props = type.GetProperties().Where(
                prop => Attribute.IsDefined(prop, typeof(ArduinoMessageMap)));

            foreach(var prop in props){
                ArduinoMessageMap amm = (ArduinoMessageMap)prop.GetCustomAttributes(true).First();
                if(amm.MessageType == message.Type)
                {
                    prop2index[prop] = amm.ArgumentIndex;
                }
            }
            map[type][message.Type] = prop2index;
        }
        
        var updatedProperties = new UpdatedProperties(obj, message);
        foreach(var kv in map[type][message.Type])
        {
            var prop2set = kv.Key;
            var argIdx = kv.Value;
            var val = message.Get(argIdx, prop2set.PropertyType);
            var oldVal = kv.Key.GetValue(obj);
            kv.Key.SetValue(obj, val);
            updatedProperties.Properties.Add(prop2set);
        }
        return updatedProperties;
    }

    public class UpdatedProperties
    {
        public ArduinoMessage? Message { get; internal set; }
        public IMessageUpdatableObject? UpdatedObject { get; internal set; }

        public List<PropertyInfo> Properties { get; internal set; } = new List<PropertyInfo>();

        public UpdatedProperties(){}
        
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
