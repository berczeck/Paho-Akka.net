namespace M2MqttAkkaNet
{
    public class PSState { }
    public class DisconnectedState : PSState { }
    public class ConnectedState : PSState { }

    public enum MqttClientStates
    {
        Connected = 0,
        Disconnected = 1
    }
}
