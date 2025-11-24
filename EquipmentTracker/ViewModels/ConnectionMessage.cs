using CommunityToolkit.Mvvm.Messaging.Messages;

namespace EquipmentTracker.ViewModels // Namespace'in burasıyla aynı olduğundan emin olun
{
    public class ConnectionMessage : ValueChangedMessage<bool>
    {
        public ConnectionMessage(bool isConnected) : base(isConnected)
        {
        }
    }
}