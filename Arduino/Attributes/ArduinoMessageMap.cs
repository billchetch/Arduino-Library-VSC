using System;
using System.Reflection;
using Chetch.Messaging;

namespace Chetch.Arduino;

[AttributeUsage(AttributeTargets.Property)]
public class ArduinoMessageMap : Attribute
{
    static Dictionary<Type, Dictionary<MessageType, Dictionary<PropertyInfo, byte>>> map = new Dictionary<Type, Dictionary<MessageType, Dictionary<PropertyInfo, byte>>>();
    
    public static void AssignMessageValues(Object obj, ArduinoMessage message)
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
        
        foreach(var kv in map[type][message.Type])
        {
            var prop2set = kv.Key;
            var argIdx = kv.Value;
            var val = message.Get(argIdx, prop2set.PropertyType);
            kv.Key.SetValue(obj, val);
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
