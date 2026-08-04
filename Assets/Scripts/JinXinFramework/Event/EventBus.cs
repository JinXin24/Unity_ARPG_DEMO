

using System;
using System.Collections.Generic;
using JinXinFramework.Event;

public static class EventBus 
{
    private static readonly Dictionary<Type,List<object>> EventDict = new();

    /// <summary>
    /// 订阅事件的方法
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须实现IEvent接口</typeparam>
    /// <param name="receiver">事件接收者,必须实现IEventReceiver<TEvent>接口</param>
    public static void Subscribe<TEvent>(IEventReceiver<TEvent> receiver) where TEvent:IEvent
    {
        //获取事件类型Type对象
        var eventType = typeof(TEvent);

        //检查事件字典中是否已存在该事件类型的接收者列表
        if(!EventDict.TryGetValue(eventType,out var receivers))
        {
            //如果不存在,则创建 一个 新的接收者列表并添加到字典中
            receivers = new List<object>();
            EventDict[eventType] = receivers;
        }
        //检查接受者是否已在当前事件的接收者列表中，避免重复订阅
        if(!receivers.Contains(receiver))
        {
            //如果不存在,则将接收者添加到列表中
            receivers.Add(receiver);
        }
    }

    /// <summary>
    /// 取消订阅指定类型的事件
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <param name="receiver"></param>
    public static void Unsubscribe<TEvent>(IEventReceiver<TEvent> receiver) where TEvent : IEvent
    {
        //获取事件类型的Type对象
        Type eventType = typeof(TEvent);

        //检查事件字典中是否已存在该事件类型的 接收者 列表
        if(EventDict.TryGetValue(eventType,out var receivers))
        {
            //如果存在,则从列表中移除接收者
            receivers.Remove(receiver);

            //如果列表为空， 则从字典中移除该事件类型的键值对
            if(receivers.Count == 0)
            {
                EventDict.Remove(eventType);
            }
        }
    }

    public static void Publish<TEvent>(TEvent evt) where TEvent : IEvent
    {
        Type eventType = typeof(TEvent);

        if(!EventDict.TryGetValue(eventType, out var receivers))
            return;

        // 拷贝快照，防止 OnEvent 内部修改列表导致遍历跳过元素
        var snapshot = new List<object>(receivers);

        for(int i = 0; i < snapshot.Count; i++)
        {
            var obj = snapshot[i];

            // 跳过已销毁的 Unity 对象
            if(obj is UnityEngine.Object unityObj && unityObj == null)
            {
                receivers.Remove(obj);
                continue;
            }

            ((IEventReceiver<TEvent>)obj).OnEvent(evt);
        }

        if(receivers.Count == 0)
            EventDict.Remove(eventType);
    }



}
