Paho-Akka.net
=========
[![Build Status](https://travis-ci.org/berczeck/Paho-Akka.net.svg)](https://travis-ci.org/berczeck/Paho-Akka.net)

## What?
Publish Subscribe library for [Akka .NET](http://getakka.net/) to pub/sub to a MQTT server. Paho-Akka.net use [M2Mqtt](https://m2mqtt.wordpress.com/) for .Net as the underlying MQTT client.

## Install?
Paho-Akka.net will be on [Nuget](https://www.nuget.org/packages/paho-akkadotnet) very soon

## How to use

```c#
class Constants
{
    public const string TopicOne = "topic/uno";
    public const string TopicTwo = "topic/dos";
}
class Program
{
static void Main(string[] args)
{
    using (var system = ActorSystem.Create("MqttSystem"))
    {
        var pubsub = 
            system.ActorOf(Props.Create(() => 
            new MqttPubSubActor(new MqttConfig("broker.hivemq.com"))), "pubsub");

        var subscriberOne = 
            system.ActorOf(Props.Create(() => 
            new SubscribeActor(pubsub, Constants.TopicOne)),
            $"subscriberOne");

        pubsub.Tell(
            new Publish(
                new Message(Constants.TopicOne, 
                $"Hello moto {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));  

        Console.WriteLine("PRESS ENTER TO CONTINUE");
        Console.ReadLine();
        subscriberOne.Tell(PoisonPill.Instance);
        pubsub.Tell(
            new Publish(
                new Message(Constants.TopicOne, 
                $"Poison {DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss")} - {Guid.NewGuid()}")));

        Console.WriteLine("PRESS ENTER TO FINISH");
        Console.ReadLine();
    }
}
}
public class SubscribeActor : ReceiveActor
{
    public SubscribeActor(IActorRef pubSub, string topic)
    {
        pubSub.Tell(new Subscribe(topic, Self));

        Receive<SubscribeAck>(x =>
        {
            if(x.IsOk())
            {
                Become(Ready);
                Context.System.Log.Info($"Subscription ok to topic {x.Subscribe.Topic}");
            }
            else
            {
                Context.System.Log.Error($"Can't subscribe to {x.Subscribe.Topic} Error: {x.Fail}");
            }
        });            
    }

    private void Ready()
    {
        Receive<Message>(x => Console.WriteLine($"Message received: {x.Topic} {x.Body} {Self.Path}"));
    }
}
```

## Licence
This software is licensed under the Apache 2 license:
http://www.apache.org/licenses/LICENSE-2.0

*Copyright 2016 Luis Alexander Aldazabal Gil (http://code2read.com/)*

*Follow me on Twitter (https://twitter.com/berczeck)*
