using System.Collections.ObjectModel;

namespace KryakApp.Services;

public sealed class ConnectedUsersStore
{
    public ObservableCollection<object> Users { get; } = [];

    public void Add(UserData user)
    {
        Users.Add(user);
    }

    public void Remove(UserData user)
    {
        Users.Remove(user);
    }

    public void Clear()
    {
        Users.Clear();
    }
}
