using System;
using System.Linq;
using System.Collections.Generic;
using Assets.Neuralyzer.Scripts.Neuralyzer.Transport;
using FlatBuffers;
using Neuralyzer.Core;
using Neuralyzer.Transport.FlatBuffers;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

/// *********************************************************************************************************************
/// This file has some implementation specific code left in the project. This was done to give examples of how the flat buffer
/// creation can be implemented. If you want it is easy to simply ignore or delete the parts you are not using. The server message factory
/// should be modified to support your unique implementation. Additionally things like server update should be modified to support any changes
/// to schema in your implementation. For more information about flat buffers go to http://google.github.io/flatbuffers/
/// *********************************************************************************************************************
namespace Neuralyzer.Transport
{
    [Serializable]
    public class RoomStateGen
    {
        public string siteDrive;
        public List<RoomObjectObj> objects;

        public RoomStateGen(RoomStateGen oldStateGen = null)
        {
            if (oldStateGen == null)
            {
                objects = new List<RoomObjectObj>();
                return;
            }
            objects = new List<RoomObjectObj>(oldStateGen.objects);
        }
    }

    [Serializable]
    public class RoomObjectObj : IBufferable<RoomObject>
    {
        public int id;
        public Vector3 position;
        public Vector3 lookDirection;
        public bool disposable;
        public string owner = "";
        public string prefab = "";
        public bool isHidden;
        public string name;

        public static bool operator ==(RoomObjectObj r1, RoomObjectObj r2)
        {
            if (Equals(r1, null))
            {
                return Equals(r2, null);
            }
            if (Equals(r2, null))
                return false;
            return r1.id == r2.id;
        }

        public static bool operator !=(RoomObjectObj r1, RoomObjectObj r2)
        {
            return !(r1 == r2);
        }

        public Offset<RoomObject> ToBuffer(FlatBufferBuilder builder)
        {
            var objOwner = builder.CreateString(owner);
            var objName = builder.CreateString(name);
            var objPrefab = builder.CreateString(prefab);
            RoomObject.StartRoomObject(builder);
            RoomObject.AddId(builder, id);
            RoomObject.AddPosition(builder,
                FlatBuffers.Vector3.CreateVector3(builder, position.x, position.y, position.z));
            RoomObject.AddLookDirection(builder, FlatBuffers.Vector3.CreateVector3(builder,
                lookDirection.x, lookDirection.y, lookDirection.z));
            RoomObject.AddDisposable(builder, disposable);
            RoomObject.AddOwner(builder, objOwner);
            RoomObject.AddName(builder, objName);
            RoomObject.AddPrefab(builder, objPrefab);
            RoomObject.AddIsHidden(builder, isHidden);
            return RoomObject.EndRoomObject(builder);
        }

        public override bool Equals(object obj)
        {
            var o = obj as RoomObjectObj;
            return o != null && o.id == id;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public static implicit operator RoomObjectObj(RoomObject fbRoomObject)
        {
            return new RoomObjectObj
            {
                id = fbRoomObject.Id,
                owner = fbRoomObject.Owner,
                disposable = fbRoomObject.Disposable,
                position = fbRoomObject.Position.Value.ToUnityVector(),
                lookDirection = fbRoomObject.LookDirection.Value.ToUnityVector(),
                prefab = fbRoomObject.Prefab,
                isHidden = fbRoomObject.IsHidden,
                name = fbRoomObject.Name
            };
        }
    }

    [Serializable]
    public class StateUpdateObject : IBufferable<StateUpdate>
    {
        public List<RoomObjectObj> create;
        public List<RoomObjectObj> update;
        public List<int> delete;

        public StateUpdateObject()
        {
            create = new List<RoomObjectObj>();
            update = new List<RoomObjectObj>();
            delete = new List<int>();
        }

        public void AddObject(RoomObjectObj newObj)
        {
            create.Add(newObj);
        }

        public void RemoveObject(RoomObjectObj remObj)
        {
            delete.Add(remObj.id);
        }

        public void UpdateObject(RoomObjectObj updObj)
        {
            if (update.Any(o => o.id == updObj.id))
            {
                update[update.IndexOf(updObj)] = updObj;
            }
            else
            {
                update.Add(updObj);
            }
        }
        
        public Offset<StateUpdate> ToBuffer(FlatBufferBuilder builder)
        {
            bool builtPoi = false;
            bool changedSiteDrive = false;
            //build all created room objects
            List<Offset<RoomObject>> roOffsets = new List<Offset<RoomObject>>();
            for (int i = 0; i < create.Count; i++)
            {
                roOffsets.Add(create[i].ToBuffer(builder));
            }
            //build all updated room objects
            List<Offset<RoomObject>> ruOffsets = new List<Offset<RoomObject>>();
            for (int i = 0; i < update.Count; i++)
            {
                ruOffsets.Add(update[i].ToBuffer(builder));
            }
            //build vectors
            VectorOffset? deleteOffset = null;
            if (delete.Count > 0)
            {
                StateUpdate.StartDeleteVector(builder, delete.Count);
                for (int i = 0; i < delete.Count; i++)
                {
                    //builder.CreateString(delete[i]);
                    builder.AddInt(delete[i]);
                }
                deleteOffset = builder.EndVector();
            }
            VectorOffset? createOffset = null;
            if (roOffsets.Count > 0)
            {
                createOffset = StateUpdate.CreateCreateVector(builder, roOffsets.ToArray());
            }
            VectorOffset? updateOffset = null;
            if (ruOffsets.Count > 0)
            {
                updateOffset = StateUpdate.CreateUpdateVector(builder, ruOffsets.ToArray());
            }
            //actually build the buffer
            StateUpdate.StartStateUpdate(builder);
            if (createOffset != null) StateUpdate.AddCreate(builder, (VectorOffset) createOffset);
            if (updateOffset != null) StateUpdate.AddUpdate(builder, (VectorOffset) updateOffset);
            if (deleteOffset != null) StateUpdate.AddDelete(builder, (VectorOffset) deleteOffset);

            var sup = StateUpdate.EndStateUpdate(builder);
            return sup;
        }
    }

    [Serializable]
    public struct TrackedObject
    {
        public bool Equals(TrackedObject other)
        {
            return string.Equals(ownerId, other.ownerId) && string.Equals(objectId, other.objectId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TrackedObject && Equals((TrackedObject) obj);
        }

        public override int GetHashCode()
        {
            return objectId.GetHashCode();
        }

        public bool isActive;
        public Vector3 position;
        public Vector3 lookDirection;
        public string ownerId;
        public string prefab;
        public int objectId;

        public static bool operator ==(TrackedObject left, TrackedObject right)
        {
            return left.objectId == right.objectId;
        }

        public static bool operator !=(TrackedObject left, TrackedObject right)
        {
            return !(left == right);
        }
    }

    [Serializable]
    public struct InitialState
    {
        public string name;
        public object[] participants;
        public object state;
        public string id;
    }

    [Serializable]
    public struct OnConnectedArgs
    {
        public string sid;
    }

    public struct CreatedEventArgs
    {
        public string name;
    }

    public struct UserLeftEventArgs
    {
        public string username;
    }

    public struct UserJoinedEventArgs
    {
        public string username;
    }

    public struct PropertiesChangedEventArgs
    {
        public static implicit operator PropertiesChangedEventArgs(StateUpdate sup)
        {
            var temp = new PropertiesChangedEventArgs();
            return temp;
        }
    }

    public struct ErrorMessageEventArgs
    {
        public string message;
    }

    /// <summary>
    /// This class facilitates the creation of server messages and isolates much of the flat buffer logic to a single place so that it is testable
    /// </summary>
    public static class ServerMessageFactory
    {
        public static byte[] BuildMessage(msgType type, string stringData)
        {
            var fbb = new FlatBufferBuilder(1024);
            switch (type)
            {
                case msgType.SocketCreateOrJoinRoom:
                    var cjString = fbb.CreateString(stringData);
                    var cjRoomOffset = StringData.CreateStringData(fbb, cjString);
                    ServerMessage.StartServerMessage(fbb);
                    ServerMessage.AddType(fbb, msgType.SocketCreateOrJoinRoom);
                    ServerMessage.AddDataType(fbb, msg.StringData);
                    ServerMessage.AddData(fbb, cjRoomOffset.Value);
                    var builtMessage = ServerMessage.EndServerMessage(fbb);
                    fbb.Finish(builtMessage.Value);
                    return fbb.SizedByteArray();
            }
            return null;
        }

        public static byte[] BuildMessage(RoomStateGen state)
        {
            var sup = new StateUpdateObject();
            return BuildMessage(sup);
        }

        public static byte[] BuildMessage(string roomName, string usrName, string usrId, string devType)
        {
            var fbb = new FlatBufferBuilder(1024);
            var rmNameOffset = fbb.CreateString(roomName);
            var usrNameOffset = fbb.CreateString(usrName);
            var usrIdOffset = fbb.CreateString(usrId);
            var devTypeOffset = fbb.CreateString(devType);
            var jcRoomOffset = JoinCreateRequest.CreateJoinCreateRequest(fbb,
                rmNameOffset, usrNameOffset, usrIdOffset, devTypeOffset);
            ServerMessage.StartServerMessage(fbb);
            ServerMessage.AddType(fbb, msgType.SocketCreateOrJoinRoom);
            ServerMessage.AddDataType(fbb, msg.JoinCreateRequest);
            ServerMessage.AddData(fbb, jcRoomOffset.Value);
            var builtMessage = ServerMessage.EndServerMessage(fbb);
            fbb.Finish(builtMessage.Value);
            return fbb.SizedByteArray();
        }

        public static byte[] BuildMessage(StateUpdateObject sup)
        {
            var fbb = new FlatBufferBuilder(1024);
            var supOffset = sup.ToBuffer(fbb);
            ServerMessage.StartServerMessage(fbb);
            ServerMessage.AddType(fbb, msgType.RoomStateUpdate);
            ServerMessage.AddDataType(fbb, msg.StateUpdate);
            ServerMessage.AddData(fbb, supOffset.Value);
            var builtMessage = ServerMessage.EndServerMessage(fbb);
            fbb.Finish(builtMessage.Value);
            return fbb.SizedByteArray();
        }

        public static byte[] BuildMessage()
        {
            var fbb = new FlatBufferBuilder(1024);
            ServerMessage.StartServerMessage(fbb);
            ServerMessage.AddType(fbb, msgType.SocketBlip);
            var builtMessage = ServerMessage.EndServerMessage(fbb);
            fbb.Finish(builtMessage.Value);
            return fbb.SizedByteArray();
        }
    }
}
