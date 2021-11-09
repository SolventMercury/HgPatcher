// HgPatcher - a universal patching format for GML.
// By https://github.com/SolventMercury
// Based off work by Jockeholm and Samuel Roy
// Uses code from https://github.com/mfascia/TexturePacker

using System;
using System.IO;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using UndertaleModLib.Util;
using UndertaleModLib.Models;
using static UndertaleModLib.Models.UndertaleSprite;

enum EventTypes {
    Create,
    Destroy,
    Alarm,
    Step,
    Collision,
    Keyboard,
    Mouse,
    Other,
    Draw,
    KeyPress,
    KeyRelease,
    Gesture,
    Asynchronous,
    PreCreate
}

void ReadGameObject(string filePath) {
	FileStream stream = File.OpenRead(filePath);
	byte[] jsonUtf8Bytes= new byte[stream.Length];

	stream.Read(jsonUtf8Bytes, 0, jsonUtf8Bytes.Length);
	stream.Close();

	JsonReaderOptions options = new JsonReaderOptions {
		AllowTrailingCommas = true,
		CommentHandling = JsonCommentHandling.Skip
	};

	Utf8JsonReader reader = new Utf8JsonReader(jsonUtf8Bytes, options);
	
	UndertaleGameObject newGameObject = new UndertaleGameObject();
	
	ReadAnticipateStartObj(ref reader);
	
	ReadObjectName(ref reader, newGameObject);
	
	if (Data.GameObjects.ByName(newGameObject.Name.Content) != null) {
		UndertaleGameObject placeholderGameObject = new UndertaleGameObject();
		placeholderGameObject.Name = Data.GameObjects.ByName(newGameObject.Name.Content).Name;
		Data.GameObjects[Data.IndexOf(Data.GameObjects.ByName(newGameObject.Name.Content))] = placeholderGameObject;
	}
	
	ReadObjectMainVals(ref reader, newGameObject);
	ReadPhysicsVerts(ref reader, newGameObject);
	ReadAllEvents(ref reader, newGameObject);
	ReadAnticipateEndObj(ref reader);
	if (Data.GameObjects.ByName(newGameObject.Name.Content) == null) {
		Data.GameObjects.Add(newGameObject);
	} else {
		Data.GameObjects[Data.IndexOf(Data.GameObjects.ByName(newGameObject.Name.Content))] = newGameObject;
	}
}

void ReadObjectMainVals(ref Utf8JsonReader reader, UndertaleGameObject newGameObject) {
	
	string spriteName            = ReadString(ref reader);
	
	newGameObject.Visible        = ReadBool(ref reader);
	newGameObject.Solid          = ReadBool(ref reader);
	newGameObject.Depth          = (int)ReadNum(ref reader);
	newGameObject.Persistent     = ReadBool(ref reader);
	
	string parentName            = ReadString(ref reader);
	string texMaskName           = ReadString(ref reader);
	
	newGameObject.UsesPhysics    = ReadBool(ref reader);
	newGameObject.IsSensor       = ReadBool(ref reader);
	newGameObject.CollisionShape = (CollisionShapeFlags)ReadNum(ref reader);
	newGameObject.Density        = ReadFloat(ref reader);
	newGameObject.Restitution    = ReadFloat(ref reader);
	newGameObject.Group          = (uint)ReadNum(ref reader);
	newGameObject.LinearDamping  = ReadFloat(ref reader);
	newGameObject.AngularDamping = ReadFloat(ref reader);
	newGameObject.Friction       = ReadFloat(ref reader);
	newGameObject.Awake          = ReadBool(ref reader);
	newGameObject.Kinematic      = ReadBool(ref reader);
	
	if (spriteName == null) {
		newGameObject.Sprite = null;
	} else {
		newGameObject.Sprite = Data.Sprites.ByName(spriteName);
	}
	
	if (parentName == null) {
		newGameObject.ParentId = null;
	} else {
		newGameObject.ParentId = Data.GameObjects.ByName(parentName);
	}
	
	if (texMaskName == null) {
		newGameObject.TextureMaskId = null;
	} else {
		newGameObject.TextureMaskId = Data.Sprites.ByName(texMaskName);
	}
}

void ReadPhysicsVerts(ref Utf8JsonReader reader, UndertaleGameObject newGameObject) {	
	newGameObject.PhysicsVertices.Clear();
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleGameObject.UndertalePhysicsVertex physVert = new UndertaleGameObject.UndertalePhysicsVertex();
			physVert.X = ReadNum(ref reader);
			physVert.Y = ReadNum(ref reader);
			newGameObject.PhysicsVertices.Add(physVert);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndObject) {
			continue;
		} 
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
}

void ReadAllEvents(ref Utf8JsonReader reader, UndertaleGameObject newGameObject) {
	
	ReadAnticipateStartArray(ref reader);
	int eventListIndex = -1;
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartArray) {
			eventListIndex++;
			newGameObject.Events[eventListIndex].Clear();
			foreach (UndertaleGameObject.Event eventToAdd in ReadEvents(ref reader)) {
				newGameObject.Events[eventListIndex].Add(eventToAdd);
			}
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndObject) {
			continue;
		} 
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
}

List<UndertaleGameObject.Event> ReadEvents(ref Utf8JsonReader reader) {
	List<UndertaleGameObject.Event> eventsToReturn = new List<UndertaleGameObject.Event>();
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			return eventsToReturn;
		}
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleGameObject.Event newEvent = new UndertaleGameObject.Event();
			newEvent.EventSubtype = (uint)ReadNum(ref reader);
			newEvent.Actions.Clear();
			foreach (UndertaleGameObject.EventAction action in ReadActions(ref reader)) {
				newEvent.Actions.Add(action);
			}
			eventsToReturn.Add(newEvent);
			ReadAnticipateEndObj(ref reader);
		}
	}
	throw new Exception("ERROR: Could not find end of array token - Events.");
}

List<UndertaleGameObject.EventAction> ReadActions(ref Utf8JsonReader reader) {
	List<UndertaleGameObject.EventAction> actionsToReturn = new List<UndertaleGameObject.EventAction>();
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			return actionsToReturn;
		}
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleGameObject.EventAction newAction = ReadAction(ref reader);
			actionsToReturn.Add(newAction);
		}
	}
	throw new Exception("ERROR: Could not find end of array token - Actions.");
}

UndertaleGameObject.EventAction ReadAction(ref Utf8JsonReader reader) {
	UndertaleGameObject.EventAction newAction = new UndertaleGameObject.EventAction();
	newAction.LibID         = (uint)ReadNum(ref reader);
	newAction.ID            = (uint)ReadNum(ref reader);
	newAction.Kind          = (uint)ReadNum(ref reader);
	newAction.UseRelative   = ReadBool(ref reader);
	newAction.IsQuestion    = ReadBool(ref reader);
	newAction.UseApplyTo    = ReadBool(ref reader);
	newAction.ExeType       = (uint)ReadNum(ref reader);
	string actionName       = ReadString(ref reader);
	string codeId           = ReadString(ref reader);
	newAction.ArgumentCount = (uint)ReadNum(ref reader);
	newAction.Who           = (int)ReadNum(ref reader);
	newAction.Relative      = ReadBool(ref reader);
	newAction.IsNot         = ReadBool(ref reader);
	
	if (actionName == null) {
		newAction.ActionName  = null;
	} else {
		newAction.ActionName  = new UndertaleString(actionName);
	}
	
	if (codeId == null) {
		newAction.CodeId  = null;
	} else {
		newAction.CodeId  = Data.Code.ByName(codeId);
	}
	
	ReadAnticipateEndObj(ref reader);
	return newAction;
}

void ReadObjectName(ref Utf8JsonReader reader, UndertaleGameObject newGameObject) {
	string name = ReadString(ref reader);
	if (name == null) {
		throw new Exception("ERROR: Object name was null - object name must be defined!");
	}
	newGameObject.Name = new UndertaleString(name);
}

void ReadRoom(string filePath) {
	FileStream stream = File.OpenRead(filePath);
	byte[] jsonUtf8Bytes= new byte[stream.Length];

	stream.Read(jsonUtf8Bytes, 0, jsonUtf8Bytes.Length);
	stream.Close();

	JsonReaderOptions options = new JsonReaderOptions {
		AllowTrailingCommas = true,
		CommentHandling = JsonCommentHandling.Skip
	};

	Utf8JsonReader reader = new Utf8JsonReader(jsonUtf8Bytes, options);
	
	UndertaleRoom newRoom = new UndertaleRoom();
	
	ReadAnticipateStartObj(ref reader);
	
	ReadRoomName(ref reader, newRoom);
	
	if (Data.Rooms.ByName(newRoom.Name.Content) != null) {
		UndertaleRoom placeholderRoom = new UndertaleRoom();
		placeholderRoom.Name = Data.Rooms.ByName(newRoom.Name.Content).Name;
		Data.Rooms[Data.IndexOf(Data.Rooms.ByName(newRoom.Name.Content))] = placeholderRoom;
	}
	
	ReadRoomMainVals(ref reader, newRoom);
	
	ReadBackgrounds(ref reader, newRoom);
	ReadViews(ref reader, newRoom);
	ReadGameObjects(ref reader, newRoom);
	ReadTiles(ref reader, newRoom);
	ReadLayers(ref reader, newRoom);
	
	ReadAnticipateEndObj(ref reader);
	if (Data.Rooms.ByName(newRoom.Name.Content) == null) {
		Data.Rooms.Add(newRoom);
	} else {
		Data.Rooms[Data.IndexOf(Data.Rooms.ByName(newRoom.Name.Content))] = newRoom;
	}
}

void ReadRoomMainVals(ref Utf8JsonReader reader, UndertaleRoom newRoom) {
	
	string caption             = ReadString(ref reader);
	
	newRoom.Width               = (uint)ReadNum(ref reader);
	newRoom.Height              = (uint)ReadNum(ref reader);
	newRoom.Speed               = (uint)ReadNum(ref reader);
	newRoom.Persistent          = ReadBool(ref reader);
	newRoom.BackgroundColor     = (uint)ReadNum(ref reader);
	newRoom.DrawBackgroundColor = ReadBool(ref reader);
	
	string ccIdName             = ReadString(ref reader);
	
	newRoom.Flags               = (UndertaleRoom.RoomEntryFlags)ReadNum(ref reader);
	newRoom.World               = ReadBool(ref reader);
	newRoom.Top                 = (uint)ReadNum(ref reader);
	newRoom.Left                = (uint)ReadNum(ref reader);
	newRoom.Right               = (uint)ReadNum(ref reader);
	newRoom.Bottom              = (uint)ReadNum(ref reader);
	newRoom.GravityX            = ReadFloat(ref reader);
	newRoom.GravityY            = ReadFloat(ref reader);
	newRoom.MetersPerPixel      = ReadFloat(ref reader);
	
	if (caption == null) {
		newRoom.Caption = null;
	} else {
		newRoom.Caption = new UndertaleString(caption);
	}
	
	if (ccIdName == null) {
		newRoom.CreationCodeId = null;
	} else {
		newRoom.CreationCodeId = Data.Code.ByName(ccIdName);
	}
}

void ReadRoomName(ref Utf8JsonReader reader, UndertaleRoom newRoom) {
	
	string name = ReadString(ref reader);
	if (name == null) {
		throw new Exception("ERROR: Object name was null - object name must be defined!");
	}
	newRoom.Name = new UndertaleString(name);
}

void ReadBackgrounds (ref Utf8JsonReader reader, UndertaleRoom newRoom) {
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.Background newBg = new UndertaleRoom.Background();
			
			newBg.ParentRoom = newRoom;
			
			newBg.CalcScaleX = ReadFloat(ref reader);
			newBg.CalcScaleY = ReadFloat(ref reader);
			newBg.Enabled    = ReadBool(ref reader);
			newBg.Foreground = ReadBool(ref reader);
			string bgDefName = ReadString(ref reader);
			newBg.X          = (int)ReadNum(ref reader);
			newBg.Y          = (int)ReadNum(ref reader);
			newBg.TileX      = (int)ReadNum(ref reader);
			newBg.TileY      = (int)ReadNum(ref reader);
			newBg.SpeedX     = (int)ReadNum(ref reader);
			newBg.SpeedY     = (int)ReadNum(ref reader);
			newBg.Stretch    = ReadBool(ref reader);
			
			if (bgDefName == null) {
				newBg.BackgroundDefinition = null;
			} else {
				newBg.BackgroundDefinition = Data.Backgrounds.ByName(bgDefName);
			}
			
			ReadAnticipateEndObj(ref reader);
			
			newRoom.Backgrounds.Add(newBg);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
}

void ReadViews (ref Utf8JsonReader reader, UndertaleRoom newRoom) {
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.View newView = new UndertaleRoom.View();
			
			
			newView.Enabled    = ReadBool(ref reader);
			newView.ViewX      = (int)ReadNum(ref reader);
			newView.ViewY      = (int)ReadNum(ref reader);
			newView.ViewWidth  = (int)ReadNum(ref reader);
			newView.ViewHeight = (int)ReadNum(ref reader);
			newView.PortX      = (int)ReadNum(ref reader);
			newView.PortY      = (int)ReadNum(ref reader);
			newView.PortWidth  = (int)ReadNum(ref reader);
			newView.PortHeight = (int)ReadNum(ref reader);
			newView.BorderX    = (uint)ReadNum(ref reader);
			newView.BorderY    = (uint)ReadNum(ref reader);
			newView.SpeedX     = (int)ReadNum(ref reader);
			newView.SpeedY     = (int)ReadNum(ref reader);
			string objIdName   = ReadString(ref reader);
			
			if (objIdName == null) {
				newView.ObjectId = null;
			} else {
				newView.ObjectId = Data.GameObjects.ByName(objIdName);
			}
			
			ReadAnticipateEndObj(ref reader);
			
			newRoom.Views.Add(newView);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
}

void ReadGameObjects (ref Utf8JsonReader reader, UndertaleRoom newRoom) {
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.GameObject newObj = new UndertaleRoom.GameObject();
			
			
			newObj.X = (int)ReadNum(ref reader);
			newObj.Y = (int)ReadNum(ref reader);
			
			string objDefName  = ReadString(ref reader);
			
			newObj.InstanceID  = (uint)ReadNum(ref reader);
			
			string ccIdName    = ReadString(ref reader);
			
			newObj.ScaleX      = ReadFloat(ref reader);
			newObj.ScaleY      = ReadFloat(ref reader);
			newObj.Color       = (uint)ReadNum(ref reader);
			newObj.Rotation    = ReadFloat(ref reader);
			
			string preCcIdName = ReadString(ref reader);
			
			newObj.ImageSpeed  = ReadFloat(ref reader);
			newObj.ImageIndex  = (int)ReadNum(ref reader);
			
			if (objDefName == null) {
				newObj.ObjectDefinition = null;
			} else {
				newObj.ObjectDefinition = Data.GameObjects.ByName(objDefName);
			}
			
			if (ccIdName == null) {
				newObj.CreationCode = null;
			} else {
				newObj.CreationCode = Data.Code.ByName(ccIdName);
			}
			
			if (preCcIdName == null) {
				newObj.PreCreateCode = null;
			} else {
				newObj.PreCreateCode = Data.Code.ByName(preCcIdName);
			}
			
			ReadAnticipateEndObj(ref reader);
			
			newRoom.GameObjects.Add(newObj);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
}

void ReadTiles (ref Utf8JsonReader reader, UndertaleRoom newRoom) {
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.Tile newTile = new UndertaleRoom.Tile();
			
			
			newTile._SpriteMode = ReadBool(ref reader);
			newTile.X           = (int)ReadNum(ref reader);
			newTile.Y           = (int)ReadNum(ref reader);
			
			string bgDefName    = ReadString(ref reader);
			string sprDefName   = ReadString(ref reader);
			
			newTile.SourceX     = (uint)ReadNum(ref reader);
			newTile.SourceY     = (uint)ReadNum(ref reader);
			newTile.Width       = (uint)ReadNum(ref reader);
			newTile.Height      = (uint)ReadNum(ref reader);
			newTile.TileDepth   = (int)ReadNum(ref reader);
			newTile.InstanceID  = (uint)ReadNum(ref reader);
			newTile.ScaleX      = ReadFloat(ref reader);
			newTile.ScaleY      = ReadFloat(ref reader);
			newTile.Color       = (uint)ReadNum(ref reader);

			if (bgDefName == null) {
				newTile.BackgroundDefinition = null;
			} else {
				newTile.BackgroundDefinition = Data.Backgrounds.ByName(bgDefName);
			}
			
			if (sprDefName == null) {
				newTile.SpriteDefinition = null;
			} else {
				newTile.SpriteDefinition = Data.Sprites.ByName(sprDefName);
			}
			
			ReadAnticipateEndObj(ref reader);
			
			newRoom.Tiles.Add(newTile);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
}

void ReadLayers (ref Utf8JsonReader reader, UndertaleRoom newRoom) {
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.Layer newLayer = new UndertaleRoom.Layer();
			
			
			string layerName   = ReadString(ref reader);
			
			newLayer.LayerId    = (uint)ReadNum(ref reader);
			newLayer.LayerType  = (UndertaleRoom.LayerType)ReadNum(ref reader);
			newLayer.LayerDepth = (int)ReadNum(ref reader);
			newLayer.XOffset    = ReadFloat(ref reader);
			newLayer.YOffset    = ReadFloat(ref reader);
			newLayer.HSpeed     = ReadFloat(ref reader);
			newLayer.VSpeed     = ReadFloat(ref reader);
			newLayer.IsVisible  = ReadBool(ref reader);


			if (layerName == null) {
				newLayer.LayerName = null;
			} else {
				newLayer.LayerName = new UndertaleString(layerName);
			}
			
			
			switch(newLayer.LayerType) {
				case UndertaleRoom.LayerType.Background:
					ReadBackgroundLayer(ref reader, newLayer);
					break;
				case UndertaleRoom.LayerType.Instances:
					ReadInstancesLayer(ref reader, newLayer);
					break;
				case UndertaleRoom.LayerType.Assets:
					ReadAssetsLayer(ref reader, newLayer);
					break;
				case UndertaleRoom.LayerType.Tiles:
					ReadTilesLayer(ref reader, newLayer);
					break;
				default:
					throw new Exception("ERROR: Invalid value for layer data type.");
					break;
			}
			
			ReadAnticipateEndObj(ref reader);
			
			newRoom.Layers.Add(newLayer);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
}

void ReadBackgroundLayer(ref Utf8JsonReader reader, UndertaleRoom.Layer newLayer) {
	ReadAnticipateStartObj(ref reader);
	
	UndertaleRoom.Layer.LayerBackgroundData newLayerData = new UndertaleRoom.Layer.LayerBackgroundData();
	
	newLayerData.CalcScaleX         = ReadFloat(ref reader);
	newLayerData.CalcScaleY         = ReadFloat(ref reader);
	newLayerData.Visible            = ReadBool(ref reader);
	newLayerData.Foreground         = ReadBool(ref reader);
	
	string spriteName               = ReadString(ref reader);
	
	newLayerData.TiledHorizontally  = ReadBool(ref reader);
	newLayerData.TiledVertically    = ReadBool(ref reader);
	newLayerData.Stretch            = ReadBool(ref reader);
	newLayerData.Color              = (uint)ReadNum(ref reader);
	newLayerData.FirstFrame         = ReadFloat(ref reader);
	newLayerData.AnimationSpeed     = ReadFloat(ref reader);
	newLayerData.AnimationSpeedType = (AnimationSpeedType)ReadNum(ref reader);


	if (spriteName == null) {
		newLayerData.Sprite = null;
	} else {
		newLayerData.Sprite = Data.Sprites.ByName(spriteName);
	}
	
	newLayerData.ParentLayer = newLayer;
	
	ReadAnticipateEndObj(ref reader);
	
	newLayer.Data = newLayerData;
}

void ReadInstancesLayer(ref Utf8JsonReader reader, UndertaleRoom.Layer newLayer) {
	ReadAnticipateStartObj(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.StartArray) {
			UndertaleRoom.Layer.LayerInstancesData newLayerData = new UndertaleRoom.Layer.LayerInstancesData();
			
			while (reader.Read()) {
				if (reader.TokenType == JsonTokenType.PropertyName) {
					continue;
				}
				if (reader.TokenType == JsonTokenType.StartObject) {
					UndertaleRoom.GameObject newObj = new UndertaleRoom.GameObject();
					
					newObj.X           = (int)ReadNum(ref reader);
					newObj.Y           = (int)ReadNum(ref reader);
					
					string objDefName  = ReadString(ref reader);
					
					newObj.InstanceID  = (uint)ReadNum(ref reader);
					
					string ccIdName    = ReadString(ref reader);
					
					newObj.ScaleX      = ReadFloat(ref reader);
					newObj.ScaleY      = ReadFloat(ref reader);
					newObj.Color       = (uint)ReadNum(ref reader);
					newObj.Rotation    = ReadFloat(ref reader);
					
					string preCcIdName = ReadString(ref reader);
					
					newObj.ImageSpeed  = ReadFloat(ref reader);
					newObj.ImageIndex  = (int)ReadNum(ref reader);
					
					if (objDefName == null) {
						newObj.ObjectDefinition = null;
					} else {
						newObj.ObjectDefinition = Data.GameObjects.ByName(objDefName);
					}
					
					if (ccIdName == null) {
						newObj.CreationCode = null;
					} else {
						newObj.CreationCode = Data.Code.ByName(ccIdName);
					}
					
					if (preCcIdName == null) {
						newObj.PreCreateCode = null;
					} else {
						newObj.PreCreateCode = Data.Code.ByName(preCcIdName);
					}
					
					ReadAnticipateEndObj(ref reader);
					
					newLayerData.Instances.Add(newObj);
					continue;
				}
				if (reader.TokenType == JsonTokenType.EndArray) {
					break;
				} else {
					throw new Exception(String.Format("ERROR: Did not correctly stop reading instances in instance layer", reader.TokenType));
				}
			}
			
			ReadAnticipateEndObj(ref reader);
			
			newLayer.Data = newLayerData;
			
			return;
		} else {
			throw new Exception(String.Format("ERROR: Did not correctly stop reading instances layer", reader.TokenType));
		}
	}
}

void ReadAssetsLayer(ref Utf8JsonReader reader, UndertaleRoom.Layer newLayer) {
	ReadAnticipateStartObj(ref reader);
	ReadAnticipateStartArray(ref reader);
	UndertaleRoom.Layer.LayerAssetsData newLayerData = new UndertaleRoom.Layer.LayerAssetsData();
	
	newLayerData.LegacyTiles = new UndertalePointerList<UndertaleRoom.Tile>();
	newLayerData.Sprites = new UndertalePointerList<UndertaleRoom.SpriteInstance>();
	newLayerData.Sequences = new UndertalePointerList<UndertaleRoom.SequenceInstance>();
	newLayerData.NineSlices = new UndertalePointerList<UndertaleRoom.SpriteInstance>();
	
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.Tile newTile = new UndertaleRoom.Tile();
			
			
			newTile._SpriteMode = ReadBool(ref reader);
			newTile.X           = (int)ReadNum(ref reader);
			newTile.Y           = (int)ReadNum(ref reader);
			
			string bgDefName    = ReadString(ref reader);
			string sprDefName   = ReadString(ref reader);
			
			newTile.SourceX     = (uint)ReadNum(ref reader);
			newTile.SourceY     = (uint)ReadNum(ref reader);
			newTile.Width       = (uint)ReadNum(ref reader);
			newTile.Height      = (uint)ReadNum(ref reader);
			newTile.TileDepth   = (int)ReadNum(ref reader);
			newTile.InstanceID  = (uint)ReadNum(ref reader);
			newTile.ScaleX      = ReadFloat(ref reader);
			newTile.ScaleY      = ReadFloat(ref reader);
			newTile.Color       = (uint)ReadNum(ref reader);

			if (bgDefName == null) {
				newTile.BackgroundDefinition = null;
			} else {
				newTile.BackgroundDefinition = Data.Backgrounds.ByName(bgDefName);
			}
			
			if (sprDefName == null) {
				newTile.SpriteDefinition = null;
			} else {
				newTile.SpriteDefinition = Data.Sprites.ByName(sprDefName);
			}
			
			ReadAnticipateEndObj(ref reader);
			
			newLayerData.LegacyTiles.Add(newTile);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
	
	ReadAnticipateStartArray(ref reader);	
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.SpriteInstance newSpr = new UndertaleRoom.SpriteInstance();
			
			string name               = ReadString(ref reader);
			string spriteName         = ReadString(ref reader);
			
			newSpr.X                  = (int)ReadNum(ref reader);
			newSpr.Y                  = (int)ReadNum(ref reader);
			newSpr.ScaleX             = ReadFloat(ref reader);
			newSpr.ScaleY             = ReadFloat(ref reader);
			newSpr.Color              = (uint)ReadNum(ref reader);
			newSpr.AnimationSpeed     = ReadFloat(ref reader);
			newSpr.AnimationSpeedType = (AnimationSpeedType)ReadNum(ref reader);
			newSpr.FrameIndex         = ReadFloat(ref reader);
			newSpr.Rotation           = ReadFloat(ref reader);
			
			if (name == null) {
				newSpr.Name = null;
			} else {
				newSpr.Name = new UndertaleString(name);
			}
			
			if (spriteName == null) {
				newSpr.Sprite = null;
			} else {
				newSpr.Sprite = Data.Sprites.ByName(spriteName);
			}

			ReadAnticipateEndObj(ref reader);
			
			newLayerData.Sprites.Add(newSpr);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Did not correctly stop reading instances in instance layer", reader.TokenType));
		}
	}
	
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.SequenceInstance newSeq = new UndertaleRoom.SequenceInstance();
			
			string name                 = ReadString(ref reader);
			string sequenceName         = ReadString(ref reader);
			
			newSeq.X                     = (int)ReadNum(ref reader);
			newSeq.Y                     = (int)ReadNum(ref reader);
			newSeq.ScaleX                = ReadFloat(ref reader);
			newSeq.ScaleY                = ReadFloat(ref reader);
			newSeq.Color                 = (uint)ReadNum(ref reader);
			newSeq.AnimationSpeed        = ReadFloat(ref reader);
			newSeq.AnimationSpeedType    = (AnimationSpeedType)ReadNum(ref reader);
			newSeq.FrameIndex            = ReadFloat(ref reader);
			newSeq.Rotation              = ReadFloat(ref reader);

			
			if (name == null) {
				newSeq.Name = null;
			} else {
				newSeq.Name = new UndertaleString(name);
			}
			
			if (sequenceName == null) {
				newSeq.Sequence = null;
			} else {
				newSeq.Sequence = Data.Sequences.ByName(sequenceName);
			}

			ReadAnticipateEndObj(ref reader);
			
			newLayerData.Sequences.Add(newSeq);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Did not correctly stop reading instances in instance layer", reader.TokenType));
		}
	}
	
	ReadAnticipateStartArray(ref reader);
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.StartObject) {
			UndertaleRoom.SpriteInstance newSpr = new UndertaleRoom.SpriteInstance();
			
			string name               = ReadString(ref reader);
			string spriteName         = ReadString(ref reader);
			
			newSpr.X                  = (int)ReadNum(ref reader);
			newSpr.Y                  = (int)ReadNum(ref reader);
			newSpr.ScaleX             = ReadFloat(ref reader);
			newSpr.ScaleY             = ReadFloat(ref reader);
			newSpr.Color              = (uint)ReadNum(ref reader);
			newSpr.AnimationSpeed     = ReadFloat(ref reader);
			newSpr.AnimationSpeedType = (AnimationSpeedType)ReadNum(ref reader);
			newSpr.FrameIndex         = ReadFloat(ref reader);
			newSpr.Rotation           = ReadFloat(ref reader);
			
			if (name == null) {
				newSpr.Name = null;
			} else {
				newSpr.Name = new UndertaleString(name);
			}
			
			if (spriteName == null) {
				newSpr.Sprite = null;
			} else {
				newSpr.Sprite = Data.Sprites.ByName(spriteName);
			}

			ReadAnticipateEndObj(ref reader);
			
			newLayerData.NineSlices.Add(newSpr);
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			break;
		} else {
			throw new Exception(String.Format("ERROR: Did not correctly stop reading instances in instance layer", reader.TokenType));
		}
	}
	newLayer.Data = newLayerData;
	ReadAnticipateEndObj(ref reader);
}

void ReadTilesLayer(ref Utf8JsonReader reader, UndertaleRoom.Layer newLayer) {
	ReadAnticipateStartObj(ref reader);
	UndertaleRoom.Layer.LayerTilesData newLayerData = new UndertaleRoom.Layer.LayerTilesData();
	newLayerData.TilesX = (uint)ReadNum(ref reader);
	newLayerData.TilesY = (uint)ReadNum(ref reader);
	uint[][] tileIds = new uint[newLayerData.TilesY][];
	for (int i = 0; i < newLayerData.TilesY; i++) {
		tileIds[i] = new uint[newLayerData.TilesX];
	}
	ReadAnticipateStartArray(ref reader);
	for (int y = 0; y < newLayerData.TilesY; y++) {
		ReadAnticipateStartArray(ref reader);
		for (int x = 0; x < newLayerData.TilesX; x++) {
			ReadAnticipateStartObj(ref reader);
			(tileIds[y])[x] = (uint)ReadNum(ref reader);
			ReadAnticipateEndObj(ref reader);
		}
		ReadAnticipateEndArray(ref reader);
	}
	newLayerData.TileData = tileIds;
	ReadAnticipateEndArray(ref reader);
	ReadAnticipateEndObj(ref reader);
	
	newLayer.Data = newLayerData;
}

// Read tokens of specified type

bool ReadBool(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.True) {
			return true;
		} else if (reader.TokenType == JsonTokenType.False) {
			return false;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Boolean - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected Boolean.");
}

long ReadNum(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.Number) {
			return reader.GetInt64();
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Integer - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected Integer.");
}

float ReadFloat(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.Number) {
			return reader.GetSingle();
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected Decimal - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected Decimal.");
}

string ReadString(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.String) {
			return reader.GetString();
		} else if (reader.TokenType == JsonTokenType.Null) {
			return null;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected String - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected String.");
}

// Watch for certain meta-tokens

void ReadAnticipateStartObj(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.StartObject) {
			return;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected StartObject - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected String.");
}

void ReadAnticipateEndObj(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndObject) {
			return;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected EndObject - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected String.");
}

void ReadAnticipateStartArray(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.StartArray) {
			return;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected StartArray - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected String.");
}

void ReadAnticipateEndArray(ref Utf8JsonReader reader) {
	while (reader.Read()) {
		if (reader.TokenType == JsonTokenType.PropertyName) {
			continue;
		}
		if (reader.TokenType == JsonTokenType.EndArray) {
			return;
		} else {
			throw new Exception(String.Format("ERROR: Unexpected token type. Expected EndArray - found {0}", reader.TokenType));
		}
	}
	throw new Exception("ERROR: Did not find value of expected type. Expected String.");
}

void BasicCodeImport(string srcPath) {
	string[] dirFiles = Directory.GetFiles(srcPath);
	foreach (string file in dirFiles) {
		ImportGMLFile(file, true);
	}
}

// For importing GML 2.3 code
void AdvancedCodeImport(string srcPath) {
	bool is2_3 = true;
	// Check code directory.
	
	string importFolder = srcPath;

	List<string> CodeList = new List<string>();

	if (File.Exists(importFolder + "/LookUpTable.txt")) {
		int counter = 0;  
		string line;  
		System.IO.StreamReader file = new System.IO.StreamReader(importFolder + "/" + "LookUpTable.txt");  
		while((line = file.ReadLine()) != null)
		{
			if (counter > 0)
				CodeList.Add(line);
			counter++;
		}
		file.Close();
	} else {
		ScriptError("No LookUpTable.txt!", "Error");
		return;
	}

	// Always link code.
	bool doParse = true;

	int progress = 0;
	string[] dirFiles = Directory.GetFiles(importFolder);
	bool skipGlobalScripts = true;
	bool skipGlobalScriptsPrompted = false;
	foreach (string file in dirFiles) {
		UpdateProgressBar(null, "Files", progress++, dirFiles.Length);

		string fileName = Path.GetFileName(file);
		if (!(fileName.EndsWith(".gml"))) {
			continue;
		}
		fileName = Path.GetFileNameWithoutExtension(file);
		int number;
		bool success = Int32.TryParse(fileName, out number);
		string codeName;
		if (success) {
			codeName = CodeList[number];
			fileName = codeName + ".gml";
		} else {
			ScriptError("GML file not in range of look up table!", "Error");
			return;
		}
		if (fileName.EndsWith("PreCreate_0.gml") && (Data.GeneralInfo.Major < 2)) {
			continue; // Restarts loop if file is not a valid code asset.
		}
		string gmlCode = File.ReadAllText(file);
		if (codeName.Substring(0, 17).Equals("gml_GlobalScript_") && is2_3 && (!(skipGlobalScriptsPrompted))) {
			skipGlobalScriptsPrompted = true;
			skipGlobalScripts = ScriptQuestion("Skip global scripts parsing?");
		}
		if (codeName.Substring(0, 17).Equals("gml_GlobalScript_") && is2_3 && ((skipGlobalScriptsPrompted))) {
			if (skipGlobalScripts) {
				continue;
			}
		}
		UndertaleCode code = Data.Code.ByName(codeName);
		// Should keep from adding duplicate scripts; haven't tested
		if (Data.Code.ByName(codeName) == null) { 
			code = new UndertaleCode();
			code.Name = Data.Strings.MakeString(codeName);
			Data.Code.Add(code);
		}
		if ((Data?.GeneralInfo.BytecodeVersion > 14) && (Data.CodeLocals.ByName(codeName) == null)) {
			UndertaleCodeLocals locals = new UndertaleCodeLocals();
			locals.Name = code.Name;

			UndertaleCodeLocals.LocalVar argsLocal = new UndertaleCodeLocals.LocalVar();
			argsLocal.Name = Data.Strings.MakeString("arguments");
			argsLocal.Index = 0;

			locals.Locals.Add(argsLocal);

			code.LocalsCount = 1;
			code.GenerateLocalVarDefinitions(code.FindReferencedLocalVars(), locals); // Dunno if we actually need this line, but it seems to work?
			Data.CodeLocals.Add(locals);
		}
		if (doParse) {
			// This portion links code.
			if (codeName.Substring(0, 10).Equals("gml_Script")) {
				// Add code to scripts section.
				if (Data.Scripts.ByName(codeName.Substring(11)) == null) {
					UndertaleScript scr = new UndertaleScript();
					scr.Name = Data.Strings.MakeString(codeName.Substring(11));
					scr.Code = code;
					Data.Scripts.Add(scr);
				} else {
					UndertaleScript scr = Data.Scripts.ByName(codeName.Substring(11));
					scr.Code = code;
				}
			}
			else if (codeName.Substring(0, 10).Equals("gml_Object")) {
				// Add code to object methods.
				string afterPrefix = codeName.Substring(11);
				// Dumb substring stuff, don't mess with this.
				int underCount = 0;
				string methodNumberStr = "", methodName = "", objName = "";
				for (int i = afterPrefix.Length - 1; i >= 0; i--)  {
					if (afterPrefix[i] == '_') {
						underCount++;
						if (underCount == 1) {
							methodNumberStr = afterPrefix.Substring(i + 1);
						} else if (underCount == 2) {
							objName = afterPrefix.Substring(0, i);
							methodName = afterPrefix.Substring(i + 1, afterPrefix.Length - objName.Length - methodNumberStr.Length - 2);
							break;
						}
					}
				}

				int methodNumber = Int32.Parse(methodNumberStr);
				UndertaleGameObject obj = Data.GameObjects.ByName(objName);
				if (obj == null) {
					bool doNewObj = ScriptQuestion("Object " + objName + " was not found.\nAdd new object called " + objName + "?");
					if (doNewObj) {
						UndertaleGameObject gameObj = new UndertaleGameObject();
						gameObj.Name = Data.Strings.MakeString(objName);
						Data.GameObjects.Add(gameObj);
					} else {
						try {
							Data.Code.ByName(codeName).ReplaceGML(gmlCode, Data);
						} catch (Exception ex) {
							string errorMSG = "Error in " +  codeName + ":\r\n" + ex.ToString() + "\r\nAborted";
							ScriptMessage(errorMSG);
							SetUMTConsoleText(errorMSG);
							SetFinishedMessage(false);
							return;
						}
						continue;
					}
				}

				obj = Data.GameObjects.ByName(objName);
				int eventIdx = (int)Enum.Parse(typeof(EventTypes), methodName);

				bool duplicate = false;
				try {
					foreach (UndertaleGameObject.Event evnt in obj.Events[eventIdx]) {
						foreach (UndertaleGameObject.EventAction action in evnt.Actions) {
							if (action.CodeId.Name.Content == codeName) {
								duplicate = true; 
							}
						}
					}
				} catch {
					// Something went wrong, but probably because it's trying to check something non-existent.
					// We're gonna make it so.
					// Keep going.
				}
				if (duplicate == false) {
					UndertalePointerList<UndertaleGameObject.Event> eventList = obj.Events[eventIdx];
					UndertaleGameObject.EventAction action = new UndertaleGameObject.EventAction();
					UndertaleGameObject.Event evnt = new UndertaleGameObject.Event();

					action.ActionName = code.Name;
					action.CodeId = code;
					evnt.EventSubtype = (uint)methodNumber;
					evnt.Actions.Add(action);
					eventList.Add(evnt);
				}
			}
			// Code which does not match these criteria cannot link, but are still added to the code section.
		} else {
			try {
				Data.Code.ByName(codeName).ReplaceGML(gmlCode, Data);
			} catch (Exception ex) {
				string errorMSG = "Error in " +  codeName + ":\r\n" + ex.ToString() + "\r\nAborted";
				ScriptMessage(errorMSG);
				SetUMTConsoleText(errorMSG);
				SetFinishedMessage(false);
				return;
			}
		}
	}

	HideProgressBar();
	ScriptMessage("All code files successfully imported.");
	
}

public class TextureInfo {
    public string Source;
    public int Width;
    public int Height;
}

public enum SplitType {
    Horizontal,
    Vertical,
}

public enum BestFitHeuristic {
    Area,
    MaxOneAxis,
}

public class Node {
    public Rectangle Bounds;
    public TextureInfo Texture;
    public SplitType SplitType;
}

public class Atlas {
    public int Width;
    public int Height;
    public List<Node> Nodes;
}

public class Packer {
    public List<TextureInfo> SourceTextures;
    public StringWriter Log;
    public StringWriter Error;
    public int Padding;
    public int AtlasSize;
    public bool DebugMode;
    public BestFitHeuristic FitHeuristic;
    public List<Atlas> Atlasses;

    public Packer() {
        SourceTextures = new List<TextureInfo>();
        Log = new StringWriter();
        Error = new StringWriter();
    }

    public void Process(string _SourceDir, string _Pattern, int _AtlasSize, int _Padding, bool _DebugMode) {
        Padding = _Padding;
        AtlasSize = _AtlasSize;
        DebugMode = _DebugMode;
        //1: scan for all the textures we need to pack
        ScanForTextures(_SourceDir, _Pattern);
        List<TextureInfo> textures = new List<TextureInfo>();
        textures = SourceTextures.ToList();
        //2: generate as many atlasses as needed (with the latest one as small as possible)
        Atlasses = new List<Atlas>();
        while (textures.Count > 0) {
            Atlas atlas = new Atlas();
            atlas.Width = _AtlasSize;
            atlas.Height = _AtlasSize;
            List<TextureInfo> leftovers = LayoutAtlas(textures, atlas);
            if (leftovers.Count == 0) {
                // we reached the last atlas. Check if this last atlas could have been twice smaller
                while (leftovers.Count == 0) {
                    atlas.Width /= 2;
                    atlas.Height /= 2;
                    leftovers = LayoutAtlas(textures, atlas);
                }
                // we need to go 1 step larger as we found the first size that is to small
                atlas.Width *= 2;
                atlas.Height *= 2;
                leftovers = LayoutAtlas(textures, atlas);
            }
            Atlasses.Add(atlas);
            textures = leftovers;
        }
    }

    public void SaveAtlasses(string _Destination) {
        int atlasCount = 0;
        string prefix = _Destination.Replace(Path.GetExtension(_Destination), "");
        string descFile = _Destination;
        StreamWriter tw = new StreamWriter(_Destination);
        tw.WriteLine("source_tex, atlas_tex, x, y, width, height");
        foreach (Atlas atlas in Atlasses) {
            string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);
            //1: Save images
            Image img = CreateAtlasImage(atlas);
            img.Save(atlasName, System.Drawing.Imaging.ImageFormat.Png);
            //2: save description in file
            foreach (Node n in atlas.Nodes) {
                if (n.Texture != null) {
                    tw.Write(n.Texture.Source + ", ");
                    tw.Write(atlasName + ", ");
                    tw.Write((n.Bounds.X).ToString() + ", ");
                    tw.Write((n.Bounds.Y).ToString() + ", ");
                    tw.Write((n.Bounds.Width).ToString() + ", ");
                    tw.WriteLine((n.Bounds.Height).ToString());
                }
            }
            ++atlasCount;
        }
        tw.Close();
        tw = new StreamWriter(prefix + ".log");
        tw.WriteLine("--- LOG -------------------------------------------");
        tw.WriteLine(Log.ToString());
        tw.WriteLine("--- ERROR -----------------------------------------");
        tw.WriteLine(Error.ToString());
        tw.Close();
    }

    private void ScanForTextures(string _Path, string _Wildcard) {
        DirectoryInfo di = new DirectoryInfo(_Path);
        FileInfo[] files = di.GetFiles(_Wildcard, SearchOption.AllDirectories);
        foreach (FileInfo fi in files) {
            Image img = Image.FromFile(fi.FullName);
            if (img != null) {
                if (img.Width <= AtlasSize && img.Height <= AtlasSize) {
                    TextureInfo ti = new TextureInfo();

                    ti.Source = fi.FullName;
                    ti.Width = img.Width;
                    ti.Height = img.Height;

                    SourceTextures.Add(ti);

                    Log.WriteLine("Added " + fi.FullName);
                } else {
                    Error.WriteLine(fi.FullName + " is too large to fix in the atlas. Skipping!");
                }
            }
        }
    }

    private void HorizontalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List) {
        Node n1 = new Node();
        n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
        n1.Bounds.Y = _ToSplit.Bounds.Y;
        n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
        n1.Bounds.Height = _Height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _ToSplit.Bounds.X;
        n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
        n2.Bounds.Width = _ToSplit.Bounds.Width;
        n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0) {
            _List.Add(n1);
		}
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0) {
            _List.Add(n2);
		}
    }

    private void VerticalSplit(Node _ToSplit, int _Width, int _Height, List<Node> _List) {
        Node n1 = new Node();
        n1.Bounds.X = _ToSplit.Bounds.X + _Width + Padding;
        n1.Bounds.Y = _ToSplit.Bounds.Y;
        n1.Bounds.Width = _ToSplit.Bounds.Width - _Width - Padding;
        n1.Bounds.Height = _ToSplit.Bounds.Height;
        n1.SplitType = SplitType.Vertical;
        Node n2 = new Node();
        n2.Bounds.X = _ToSplit.Bounds.X;
        n2.Bounds.Y = _ToSplit.Bounds.Y + _Height + Padding;
        n2.Bounds.Width = _Width;
        n2.Bounds.Height = _ToSplit.Bounds.Height - _Height - Padding;
        n2.SplitType = SplitType.Horizontal;
        if (n1.Bounds.Width > 0 && n1.Bounds.Height > 0) {
            _List.Add(n1);
		}
        if (n2.Bounds.Width > 0 && n2.Bounds.Height > 0) {
            _List.Add(n2);
		}
    }

    private TextureInfo FindBestFitForNode(Node _Node, List<TextureInfo> _Textures) {
        TextureInfo bestFit = null;
        float nodeArea = _Node.Bounds.Width * _Node.Bounds.Height;
        float maxCriteria = 0.0f;
        foreach (TextureInfo ti in _Textures) {
            switch (FitHeuristic) {
                // Max of Width and Height ratios
                case BestFitHeuristic.MaxOneAxis:
                    if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height) {
                        float wRatio = (float)ti.Width / (float)_Node.Bounds.Width;
                        float hRatio = (float)ti.Height / (float)_Node.Bounds.Height;
                        float ratio = wRatio > hRatio ? wRatio : hRatio;
                        if (ratio > maxCriteria) {
                            maxCriteria = ratio;
                            bestFit = ti;
                        }
                    }
                    break;
                // Maximize Area coverage
                case BestFitHeuristic.Area:
                    if (ti.Width <= _Node.Bounds.Width && ti.Height <= _Node.Bounds.Height) {
                        float textureArea = ti.Width * ti.Height;
                        float coverage = textureArea / nodeArea;
                        if (coverage > maxCriteria) {
                            maxCriteria = coverage;
                            bestFit = ti;
                        }
                    }
                    break;
            }
        }
        return bestFit;
    }

    private List<TextureInfo> LayoutAtlas(List<TextureInfo> _Textures, Atlas _Atlas) {
        List<Node> freeList = new List<Node>();
        List<TextureInfo> textures = new List<TextureInfo>();
        _Atlas.Nodes = new List<Node>();
        textures = _Textures.ToList();
        Node root = new Node();
        root.Bounds.Size = new Size(_Atlas.Width, _Atlas.Height);
        root.SplitType = SplitType.Horizontal;
        freeList.Add(root);
        while (freeList.Count > 0 && textures.Count > 0) {
            Node node = freeList[0];
            freeList.RemoveAt(0);
            TextureInfo bestFit = FindBestFitForNode(node, textures);
            if (bestFit != null) {
                if (node.SplitType == SplitType.Horizontal) {
                    HorizontalSplit(node, bestFit.Width, bestFit.Height, freeList);
                } else {
                    VerticalSplit(node, bestFit.Width, bestFit.Height, freeList);
                }
                node.Texture = bestFit;
                node.Bounds.Width = bestFit.Width;
                node.Bounds.Height = bestFit.Height;
                textures.Remove(bestFit);
            }
            _Atlas.Nodes.Add(node);
        }
        return textures;
    }

    private Image CreateAtlasImage(Atlas _Atlas) {
        Image img = new Bitmap(_Atlas.Width, _Atlas.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(img);
        foreach (Node n in _Atlas.Nodes) {
            if (n.Texture != null) {
                Image sourceImg = Image.FromFile(n.Texture.Source);
                g.DrawImage(sourceImg, n.Bounds);
            }
        }
        // DPI FIX START
        Bitmap ResolutionFix = new Bitmap(img);
        ResolutionFix.SetResolution(96.0F, 96.0F);
        Image img2 = ResolutionFix;
        return img2;
        // DPI FIX END
    }
}

public class SpriteInfo {
	public string Name;
	public int SizeX = 0;
	public int SizeY = 0;
	public int MarginLeft = 0;
	public int MarginRight = 0;
	public int MarginBottom = 0;
	public int MarginTop = 0;
	public bool Transparent = false;
	public bool Smooth = false;
	public bool Preload = false;
	public int BoundingBoxMode = 0;
	public SepMaskType SepMasks = SepMaskType.AxisAlignedRect;
	public int OriginX = 0;
	public int OriginY = 0;
	public SpriteType Type = SpriteType.Normal;
	
	// GMS 2 Exclusive
	public float PlaybackSpeed = 30;
	public AnimSpeedType Playback = AnimSpeedType.FramesPerSecond;
	// Info specifically for us
	
	public int originalNumFrames;
	public int finalNumFrames;
	public bool canOriginalFramesFitInPlace;
	
	public SpriteInfo(string name) {
		this.Name = name;
	}
}

string StripOpeningWhitespace(string line) {
	// Strip whitespace at start of a line
	char[] whitespaceChars = {'\r', '\n', ' ', '\t'}; 
	int lineStartIndex = line.Length - 1;
	if (lineStartIndex <= 0) {
		return line;
	}
	for (int i = 0; i < line.Length; i++) {
		if (!Array.Exists(whitespaceChars, element => element == line[i])) {
			lineStartIndex = i;
			break;
		}
	}
	line = line.Substring(lineStartIndex);
	return line;
}

string StripClosingWhitespace(string line) {
	// Strip whitespace at start of a line
	char[] whitespaceChars = {'\r', '\n', ' ', '\t'}; 
	int lineEndIndex = 0;
	for (int i = line.Length - 1; i >= 0; i--) {
		if (!Array.Exists(whitespaceChars, element => element == line[i])) {
			lineEndIndex = i;
			break;
		}
	}
	line = line.Substring(0, lineEndIndex + 1);
	return line;
}

bool EnsureAllFramesReplaced(string spritePath, string spriteName, int minNumFrames, int newSizeX, int newSizeY) {
	// Get list of sprites
	List<string> dirFiles = new List<string>(Directory.GetFiles(spritePath, "*.png"));
	List<string> spriteFiles = new List<string>();
	foreach (string filePath in dirFiles) {
		if (filePath.Contains("_") && filePath.Contains(spriteName)) {
			spriteFiles.Add(filePath);
		}
	}
	
	// Make sure we have enough sprites in our folder
	if (minNumFrames > spriteFiles.Count) {
		return false;
	}
	
	// Validate frames numbering
	List<string> expectedFileNames = new List<string>();
	for (int i = 0; i < spriteFiles.Count; i++) {
		StringBuilder sb = new StringBuilder(32);
		sb.Append(Path.Join(spritePath, spriteName));
		sb.Append("_");
		sb.Append(i.ToString());
		sb.Append(".png");
		string expectedFileName = sb.ToString();
		expectedFileNames.Add(expectedFileName);
	}
	
	foreach (string expectedFileName in expectedFileNames) {
		if (!spriteFiles.Contains(expectedFileName)) {
			return false;
		}
	}
	
	// Validate image sizes
	foreach (string spriteFile in spriteFiles) {
		System.Drawing.Image img = System.Drawing.Image.FromFile(spriteFile);
		if (img.Width != newSizeX || img.Height != newSizeY) {
			return false;
		}
	}
	return true;
}

int CountTotalFramesAfterPatch(string spritePath, UndertaleSprite sprite) {
	int vanillaFrameCount = sprite.Textures.Count;
	string spriteName = sprite.Name.ToString();
	List<string> dirFiles = new List<string>(Directory.GetFiles(spritePath, "*.png"));
	List<string> spriteFiles = new List<string>();
	foreach (string filePath in dirFiles) {
		if (filePath.Contains("_") && filePath.Contains(spriteName)) {
			spriteFiles.Add(filePath);
		}
	}
	
	// Get list of expected sprite names.
	// These can be either present or missing in the actual dir.
	List<string> expectedSpriteFiles = new List<string>();
	for (int i = 0; i < vanillaFrameCount; i++) {
		StringBuilder sb = new StringBuilder(32);
		sb.Append(Path.Join(spritePath, spriteName));
		sb.Append("_");
		sb.Append(i.ToString());
		sb.Append(".png");
		string expectedFileName = sb.ToString();
		expectedSpriteFiles.Add(expectedFileName);
	}
	
	// See how many expected files occur.
	int numSpriteFilesExpected = 0;
	foreach(string spriteFile in spriteFiles) {
		if (expectedSpriteFiles.Contains(spriteFile)) {
			numSpriteFilesExpected++;
		}
	}
	
	int numAddedFrames = spriteFiles.Count - numSpriteFilesExpected;
	
	// Verify correct frame numbering
	// Generate what we expect new files to be named
	List<string> expectedNewSpriteFiles = new List<string>();
	for (int i = 0; i < vanillaFrameCount; i++) {
		StringBuilder sb = new StringBuilder(32);
		sb.Append(Path.Join(spritePath, spriteName));
		sb.Append("_");
		sb.Append((i + vanillaFrameCount).ToString());
		sb.Append(".png");
		string expectedNewSpriteFileName = sb.ToString();
		expectedNewSpriteFiles.Add(expectedNewSpriteFileName);
	}
	
	// Check that these have a 100% overlap with actual filenames
	int numNewSpriteFilesExpected = 0;
	foreach(string spriteFile in spriteFiles) {
		if (expectedNewSpriteFiles.Contains(spriteFile)) {
			numNewSpriteFilesExpected++;
		}
	}
	
	if (numAddedFrames != numNewSpriteFilesExpected) {
		throw new Exception(String.Format("ERROR: Frames for sprite '{0}' have invalid format!", spriteName));
	}
	return vanillaFrameCount + numAddedFrames;
}

int CountTotalFramesForNewSprite(string spritePath, string spriteName) {
	
	List<string> dirFiles = new List<string>(Directory.GetFiles(spritePath, "*.png"));
	List<string> spriteFiles = new List<string>();
	foreach (string filePath in dirFiles) {
		if (filePath.Contains("_") && filePath.Contains(spriteName)) {
			spriteFiles.Add(filePath);
		}
	}
	
	// Get list of expected sprite names.
	List<string> expectedSpriteFiles = new List<string>();
	for (int i = 0; i < spriteFiles.Count; i++) {
		StringBuilder sb = new StringBuilder(32);
		sb.Append(Path.Join(spritePath, spriteName));
		sb.Append("_");
		sb.Append(i.ToString());
		sb.Append(".png");
		string expectedFileName = sb.ToString();
		expectedSpriteFiles.Add(expectedFileName);
	}
	
	// See how many expected files occur.
	int numSpriteFilesExpected = 0;
	foreach(string spriteFile in spriteFiles) {
		if (expectedSpriteFiles.Contains(spriteFile)) {
			numSpriteFilesExpected++;
		}
	}
	
	if (numSpriteFilesExpected != spriteFiles.Count) {
		throw new Exception(String.Format("ERROR: Frames for sprite '{0}' have invalid format!", spriteName));
	}
	return spriteFiles.Count;
}

// Creates SpriteInfo objects for each sprite we import.
// If an UndertaleSprite does not exist for our new sprites, we create it.
// It will be necessary to fill in the textures and mask later.
Dictionary<string, SpriteInfo> ImportSpriteInfo(string spriteInfoPath, bool forceMatchingSpriteSize, int sVersion) {
	string currentSpriteName = null;
	string spritePath = Path.GetDirectoryName(spriteInfoPath);
	SpriteInfo currentSpriteInfo = null;
	bool processingSprite = false;
	Dictionary<string, SpriteInfo> spriteInfoDict = new Dictionary<string, SpriteInfo>();
	if (File.Exists(spriteInfoPath)) {
		foreach (string lineIn in System.IO.File.ReadLines(spriteInfoPath)) {
			string line = StripOpeningWhitespace(lineIn);
			if (line.Length < 1) {
				continue;
			}
			// Ignore comments
			if (line[0] == '#') {
				continue;
			}
			// Handle sprite declarations
			if (line.Contains('{')) {
				processingSprite = true;
				int i = 0;
				currentSpriteName = StripClosingWhitespace(line.Split("{")[0]);
				currentSpriteInfo = new SpriteInfo(currentSpriteName);
				// Override defaults if sprite already exists
				UndertaleSprite sprite = Data.Sprites.ByName(currentSpriteName);
				if (sprite != null) {               
					currentSpriteInfo.SizeX           = (int)sprite.Width;
					currentSpriteInfo.SizeY           = (int)sprite.Height;
					currentSpriteInfo.MarginLeft      = sprite.MarginLeft;
					currentSpriteInfo.MarginRight     = sprite.MarginRight;
					currentSpriteInfo.MarginBottom    = sprite.MarginBottom;
					currentSpriteInfo.MarginTop       = sprite.MarginTop;
					currentSpriteInfo.Transparent     = sprite.Transparent;
					currentSpriteInfo.Smooth          = sprite.Smooth;
					currentSpriteInfo.Preload         = sprite.Preload;
					currentSpriteInfo.BoundingBoxMode = (int)sprite.BBoxMode;
					currentSpriteInfo.SepMasks        = sprite.SepMasks;
					currentSpriteInfo.OriginX         = sprite.OriginX;
					currentSpriteInfo.OriginY         = sprite.OriginY;
					currentSpriteInfo.Type            = sprite.SSpriteType;
					currentSpriteInfo.PlaybackSpeed   = sprite.GMS2PlaybackSpeed;
					currentSpriteInfo.Playback        = sprite.GMS2PlaybackSpeedType;
				}
				
			// Handle sprite definition end
			} else if (line.Contains('}')) {
				processingSprite = false;
				UndertaleSprite sprite = Data.Sprites.ByName(currentSpriteName);
				if (sprite == null) {
					if (currentSpriteInfo.SizeX <= 0 || currentSpriteInfo.SizeY <= 0) {
						throw new Exception(String.Format("ERROR: New sprite {0} was declared, but no size was found.", currentSpriteName));
					}
					UndertaleString spriteUTString = Data.Strings.MakeString(currentSpriteName);
					UndertaleSprite newSprite = new UndertaleSprite();
					
					// Sprite does not exist in original
					// Because a whopping 0 pixels will be replaced,
					// It technically can fit in the original.
					currentSpriteInfo.canOriginalFramesFitInPlace = true;
					currentSpriteInfo.originalNumFrames = 0;
					currentSpriteInfo.finalNumFrames = CountTotalFramesForNewSprite(spritePath, currentSpriteName);
					
					newSprite.Name                  = spriteUTString;
					newSprite.Width                 = (uint)currentSpriteInfo.SizeX;
					newSprite.Height                = (uint)currentSpriteInfo.SizeY;
					newSprite.MarginLeft            = currentSpriteInfo.MarginLeft;
					newSprite.MarginRight           = currentSpriteInfo.MarginRight;
					newSprite.MarginTop             = currentSpriteInfo.MarginTop;
					newSprite.MarginBottom          = currentSpriteInfo.MarginBottom;
					newSprite.OriginX               = currentSpriteInfo.OriginX;
					newSprite.OriginY               = currentSpriteInfo.OriginY;
					newSprite.Smooth                = currentSpriteInfo.Smooth;
					newSprite.Transparent           = currentSpriteInfo.Transparent;
					newSprite.Preload               = currentSpriteInfo.Preload;
					newSprite.SSpriteType           = currentSpriteInfo.Type;
					newSprite.BBoxMode              = (uint)currentSpriteInfo.BoundingBoxMode;
					newSprite.SepMasks              = currentSpriteInfo.SepMasks;
					newSprite.GMS2PlaybackSpeed     = currentSpriteInfo.PlaybackSpeed;
					newSprite.GMS2PlaybackSpeedType = currentSpriteInfo.Playback;
					
					newSprite.SVersion = (uint)sVersion;
					if (sVersion >= 2) {
						newSprite.IsSpecialType = true;
					} else {
						newSprite.IsSpecialType = false;
					}
					
					Data.Sprites.Add(newSprite);
				} else {
					if (currentSpriteInfo.SizeX != 0 || currentSpriteInfo.SizeY != 0) {
						if (currentSpriteInfo.SizeX != sprite.Textures[0].Texture.TargetWidth || currentSpriteInfo.SizeY != sprite.Textures[0].Texture.TargetHeight) {
							if (forceMatchingSpriteSize) {
								throw new Exception(String.Format("ERROR: Patch set config option forceMatchingSpriteSize to true, but sprite size given for {0} doesn't match the size of the original sprite!", currentSpriteName));
							}
							if (!EnsureAllFramesReplaced(spritePath, currentSpriteName, sprite.Textures.Count, currentSpriteInfo.SizeX, currentSpriteInfo.SizeY)) {
								throw new Exception(String.Format("ERROR: New sprite {0} has differing dimensions to the original, but did not replace every frame.", currentSpriteName));
							}
						}
					}
					
					currentSpriteInfo.canOriginalFramesFitInPlace = (currentSpriteInfo.SizeX <= sprite.Width && currentSpriteInfo.SizeY <= sprite.Height);
					currentSpriteInfo.originalNumFrames = sprite.Textures.Count;
					currentSpriteInfo.finalNumFrames = CountTotalFramesAfterPatch(spritePath, sprite);
					
					sprite.Width                 = (uint)currentSpriteInfo.SizeX;
					sprite.Height                = (uint)currentSpriteInfo.SizeY;
					sprite.MarginLeft            = currentSpriteInfo.MarginLeft;
					sprite.MarginRight           = currentSpriteInfo.MarginRight;
					sprite.MarginTop             = currentSpriteInfo.MarginTop;
					sprite.MarginBottom          = currentSpriteInfo.MarginBottom;
					sprite.OriginX               = currentSpriteInfo.OriginX;
					sprite.OriginY               = currentSpriteInfo.OriginY;
					sprite.Smooth                = currentSpriteInfo.Smooth;
					sprite.Transparent           = currentSpriteInfo.Transparent;
					sprite.Preload               = currentSpriteInfo.Preload;
					sprite.SSpriteType           = currentSpriteInfo.Type;
					sprite.BBoxMode              = (uint)currentSpriteInfo.BoundingBoxMode;
					sprite.SepMasks              = currentSpriteInfo.SepMasks;
					sprite.GMS2PlaybackSpeed     = currentSpriteInfo.PlaybackSpeed;
					sprite.GMS2PlaybackSpeedType = currentSpriteInfo.Playback;
				}
				// Add to dict
				spriteInfoDict.Add(currentSpriteName, currentSpriteInfo);
				continue;
			// Handle sprite parameters
			} else if (line.Contains(':')) {
				string paramName = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[0]));
				string paramVal = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[1]));
				
				if (paramName.Equals("size_x")) {
					currentSpriteInfo.SizeX = Int32.Parse(paramVal);
				} else if (paramName.Equals("size_y")) {
					currentSpriteInfo.SizeY = Int32.Parse(paramVal);
				} else if (paramName.Equals("margin_left")) {
					currentSpriteInfo.MarginLeft = Int32.Parse(paramVal);
				} else if (paramName.Equals("margin_right")) {
					currentSpriteInfo.MarginRight = Int32.Parse(paramVal);
				} else if (paramName.Equals("margin_bottom")) {
					currentSpriteInfo.MarginBottom = Int32.Parse(paramVal);
				} else if (paramName.Equals("margin_top")) {
					currentSpriteInfo.MarginTop = Int32.Parse(paramVal);
				} else if (paramName.Equals("transparent")) {
					currentSpriteInfo.Transparent = Boolean.Parse(paramVal);
				} else if (paramName.Equals("smooth")) {
					currentSpriteInfo.Smooth = Boolean.Parse(paramVal);
				} else if (paramName.Equals("preload")) {
					currentSpriteInfo.Preload = Boolean.Parse(paramVal);
				} else if (paramName.Equals("bounding_box_mode")) {
					currentSpriteInfo.BoundingBoxMode = Int32.Parse(paramVal);
				} else if (paramName.Equals("sep_masks")) {
					currentSpriteInfo.SepMasks = (SepMaskType) Enum.Parse(typeof(SepMaskType), paramVal, true);
				} else if (paramName.Equals("origin_x")) {
					currentSpriteInfo.OriginX = Int32.Parse(paramVal);
				} else if (paramName.Equals("origin_y")) {
					currentSpriteInfo.OriginY = Int32.Parse(paramVal);
				} else if (paramName.Equals("playback_speed")) {
					currentSpriteInfo.PlaybackSpeed = Single.Parse(paramVal);
				} else if (paramName.Equals("playback")) {
					currentSpriteInfo.Playback = (AnimSpeedType) Enum.Parse(typeof(AnimSpeedType), paramVal, true);
				} else {
					throw new Exception(String.Format("ERROR: Parameter '{0}' was declared, but '{0}' is not a valid sprite parameter.", paramName));
				}
			// Ignore empty lines
			} else {
				continue;
			}
		}
	}
		
	// Now go through all .png files in our sprite directory to supplement our SpriteInfo dict, for completeness' sake.
	string[] spriteFiles = Directory.GetFiles(spritePath, "*.png");
	foreach (string spriteFile in spriteFiles) {
		// Get sprite name and frame number for filename
		string imageName = Path.GetFileNameWithoutExtension(spriteFile);
		int lastUnderscore = imageName.LastIndexOf('_');
		string spriteName = imageName.Substring(0, lastUnderscore);
		int frameNumber = -1;
		try {
			frameNumber = Int32.Parse(imageName.Substring(lastUnderscore + 1));
		} catch {
			throw new Exception(String.Format("ERROR: Sprite file at {0} does not have a valid frame number!", spriteFile));
		}
		
		// We found a sprite that wasn't explicitly defined
		if (!spriteInfoDict.ContainsKey(spriteName)) {
			SpriteInfo infoToAdd = new SpriteInfo(spriteName);
			UndertaleSprite sprite = Data.Sprites.ByName(spriteName);
			if (sprite != null) {               
				infoToAdd.MarginLeft      = sprite.MarginLeft;
				infoToAdd.MarginRight     = sprite.MarginRight;
				infoToAdd.MarginBottom    = sprite.MarginBottom;
				infoToAdd.MarginTop       = sprite.MarginTop;
				infoToAdd.Transparent     = sprite.Transparent;
				infoToAdd.Smooth          = sprite.Smooth;
				infoToAdd.Preload         = sprite.Preload;
				infoToAdd.BoundingBoxMode = (int)sprite.BBoxMode;
				infoToAdd.SepMasks        = sprite.SepMasks;
				infoToAdd.OriginX         = sprite.OriginX;
				infoToAdd.OriginY         = sprite.OriginY;
				infoToAdd.Type            = sprite.SSpriteType;
				infoToAdd.PlaybackSpeed   = sprite.GMS2PlaybackSpeed;
				infoToAdd.Playback        = sprite.GMS2PlaybackSpeedType;
			}
			
			// Get size from the first image we got of this sprite
			System.Drawing.Image img = System.Drawing.Image.FromFile(spriteFile);
			infoToAdd.SizeX = img.Width;
			infoToAdd.SizeY = img.Height;
			
			// Perform frame replacement check, to make sure we always get consistent sprite sizes.
			// Note that we don't check that sprite images have the same size as one another until texture import stage.
			if (sprite != null) {
				if (infoToAdd.SizeX != sprite.Textures[0].Texture.TargetWidth || infoToAdd.SizeY != sprite.Textures[0].Texture.TargetHeight) {
					if (forceMatchingSpriteSize) {
						throw new Exception(String.Format("ERROR: Patch set config option forceMatchingSpriteSize to true, but sprite file {0} doesn't match the size of the original sprite!", spriteFile));
					}
					if (!EnsureAllFramesReplaced(spritePath, spriteName, sprite.Textures.Count, infoToAdd.SizeX, infoToAdd.SizeY)) {
						throw new Exception(String.Format("ERROR: Sprite {0} changed size, but not all frames are replaced!", spriteName));
					} else {
						infoToAdd.canOriginalFramesFitInPlace = (infoToAdd.SizeX <= sprite.Textures[0].Texture.TargetWidth || infoToAdd.SizeY <= sprite.Textures[0].Texture.TargetHeight);
					}
				} else {
					infoToAdd.canOriginalFramesFitInPlace = true;
				}
				infoToAdd.originalNumFrames = sprite.Textures.Count;
				infoToAdd.finalNumFrames = CountTotalFramesAfterPatch(spritePath, sprite);
			} else {
				infoToAdd.canOriginalFramesFitInPlace = true;
				infoToAdd.originalNumFrames = 0;
				infoToAdd.finalNumFrames = CountTotalFramesForNewSprite(spritePath, currentSpriteName);
				
				// Create new sprite if none exists
				UndertaleString spriteUTString = Data.Strings.MakeString(currentSpriteName);
				UndertaleSprite newSprite = new UndertaleSprite();
				
				// Sprite does not exist in original
				// Because a whopping 0 pixels will be replaced,
				// It technically can fit in the original.
				infoToAdd.canOriginalFramesFitInPlace = true;
				infoToAdd.originalNumFrames = 0;
				infoToAdd.finalNumFrames = CountTotalFramesForNewSprite(spritePath, currentSpriteName);
				
				newSprite.Name                  = spriteUTString;
				newSprite.Width                 = (uint)infoToAdd.SizeX;
				newSprite.Height                = (uint)infoToAdd.SizeY;
				newSprite.MarginLeft            = infoToAdd.MarginLeft;
				newSprite.MarginRight           = infoToAdd.MarginRight;
				newSprite.MarginTop             = infoToAdd.MarginTop;
				newSprite.MarginBottom          = infoToAdd.MarginBottom;
				newSprite.OriginX               = infoToAdd.OriginX;
				newSprite.OriginY               = infoToAdd.OriginY;
				newSprite.Smooth                = infoToAdd.Smooth;
				newSprite.Transparent           = infoToAdd.Transparent;
				newSprite.Preload               = infoToAdd.Preload;
				newSprite.SSpriteType           = infoToAdd.Type;
				newSprite.BBoxMode              = (uint)infoToAdd.BoundingBoxMode;
				newSprite.SepMasks              = infoToAdd.SepMasks;
				newSprite.GMS2PlaybackSpeed     = infoToAdd.PlaybackSpeed;
				newSprite.GMS2PlaybackSpeedType = infoToAdd.Playback;
				
				newSprite.SVersion = (uint)sVersion;
				if (sVersion >= 2) {
					newSprite.IsSpecialType = true;
				} else {
					newSprite.IsSpecialType = false;
				}
				
				Data.Sprites.Add(newSprite);
			}
			
			
			
			// Update info dict.
			// Note that we only do all this processing once per sprite,
			// As adding this to the dict means that future frames of the same sprite will be ignored.
			spriteInfoDict[spriteName] = infoToAdd;
		}
	}
	return spriteInfoDict;
}

enum TextureType {
	Sprite,
	Background,
	Font,
}

class TextureToPack {
	public string Name;
	public string Path;
	public TextureType Type;
	
	public TextureToPack(string name, string path, TextureType type) {
		this.Name = name;
		this.Path = path;
		this.Type = type;
	}
}

void ImportTextures(string spritePath, string bgPath, string fntPath, Dictionary<string, SpriteInfo> spriteInfoDict, bool forceMatchingSpriteSize) {
	// Get list of all files
	string[] spriteFiles = Directory.GetFiles(spritePath, "*.png");
	string[] bgFiles = Directory.GetFiles(bgPath, "*.png");
	string[] fntFiles = Directory.GetFiles(fntPath, "*.png");
	
	// If we can't (or shoudln't) fit something into existing texture sheets, we should put it here.
	// Otherwise, we assume that texture data has simply overwritten existing textures.
	Dictionary<string, TextureToPack> texturesToPack = new Dictionary<string, TextureToPack>();
	List<string> textureNames = new List<string>();
	
	// Process them individually, storing up TextureToPack's for sprites which can't be placed in original locations.
	foreach (string spriteFile in spriteFiles) {
		// Get sprite name and frame number for filename
		string imageName = Path.GetFileNameWithoutExtension(spriteFile);
		int lastUnderscore = imageName.LastIndexOf('_');
		string spriteName = imageName.Substring(0, lastUnderscore);
		int frameNumber = -1;
		try {
			frameNumber = Int32.Parse(imageName.Substring(lastUnderscore + 1));
		} catch {
			throw new Exception(String.Format("ERROR: Sprite file at {0} does not have a valid frame number!", spriteFile));
		}
		
		// Get sprite
		// Sprites should already exist from ImportSpriteInfo
		UndertaleSprite currentSprite = Data.Sprites.ByName(spriteName);
		if (currentSprite == null) {
			throw new Exception(String.Format("ERROR: Could not generate a sprite entry for the sprite file {0}!", spriteFile));
		}
		
		// Get spriteInfo for this sprite
		SpriteInfo spriteInfo = spriteInfoDict[spriteName];
		
		// If sprite info calls for more frames than currently exist, extend texture entry list with placeholders.
		if (spriteInfo.finalNumFrames > currentSprite.Textures.Count) {
			int numFramesToAdd = spriteInfo.finalNumFrames - currentSprite.Textures.Count;
			for (int i = 0; i < numFramesToAdd; i++) {
				currentSprite.Textures.Add(null);
			}
		}
		
		// Decide what to do with this sprite.
		if (spriteInfo.canOriginalFramesFitInPlace && frameNumber < spriteInfo.originalNumFrames) {
			// Write directly to texture
			Bitmap bmp;
            using (var ms = new MemoryStream(TextureWorker.ReadTextureBlob(spriteFile))) {
                bmp = new Bitmap(ms);
            }
            bmp.SetResolution(96.0F, 96.0F);
			// If bitmap is smaller than original, copy it onto a bigger image first.
			if (bmp.Width < currentSprite.Width || bmp.Height < currentSprite.Height) {
				Bitmap destBmp = new Bitmap((int)currentSprite.Width, (int)currentSprite.Height);
				destBmp.SetResolution(96.0F, 96.0F);
				using (Graphics grD = Graphics.FromImage(destBmp)) {
					grD.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
				}
				bmp = destBmp;
			}
			currentSprite.Textures[frameNumber].Texture.ReplaceTexture((Image)bmp);
		} else {
			// Queue up for later
			textureNames.Add(spriteName);
			texturesToPack.Add(spriteName, new TextureToPack(spriteName, spriteFile, TextureType.Sprite));
		}
	}
	
	// Now do Fonts and Backgrounds.
	// In terms of texture handling, they're basically the same.
	
	foreach (string bgFile in bgFiles) {
		string bgName = Path.GetFileNameWithoutExtension(bgFile);
		UndertaleBackground currentBg = Data.Backgrounds.ByName(bgName);
		if (currentBg.Texture == null) {
			textureNames.Add(bgName);
			texturesToPack.Add(bgName, new TextureToPack(bgName, bgFile, TextureType.Background));
		} else {
		
			Bitmap bmp;
			using (var ms = new MemoryStream(TextureWorker.ReadTextureBlob(bgFile))) {
				bmp = new Bitmap(ms);
			}
			bmp.SetResolution(96.0F, 96.0F);
			
			if ((bmp.Width == currentBg.Texture.BoundingWidth && bmp.Height == currentBg.Texture.BoundingHeight)) {
				currentBg.Texture.ReplaceTexture((Image)bmp);
			} else {
				if (forceMatchingSpriteSize) {
					throw new Exception(String.Format("ERROR: forceMatchingSpriteSize has been configured to be true, but background {0} has a non-matching sprite size!", bgFile));
				}
				if (bmp.Width <= currentBg.Texture.BoundingWidth && bmp.Height <= currentBg.Texture.BoundingHeight) {
					Bitmap destBmp = new Bitmap((int)currentBg.Texture.BoundingWidth, (int)currentBg.Texture.BoundingHeight);
					destBmp.SetResolution(96.0F, 96.0F);
					using (Graphics grD = Graphics.FromImage(destBmp)) {
						grD.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
					}
					bmp = destBmp;
					currentBg.Texture.ReplaceTexture((Image)bmp);
				} else if (bmp.Width > currentBg.Texture.BoundingWidth || bmp.Height > currentBg.Texture.BoundingHeight) {
					textureNames.Add(bgName);
					texturesToPack.Add(bgName, new TextureToPack(bgName, bgFile, TextureType.Background));
				}
			}
		}
	}
	
	foreach (string fntFile in fntFiles) {
		string fntName = Path.GetFileNameWithoutExtension(fntFile);
		UndertaleFont currentFnt = Data.Fonts.ByName(fntName);
		if (currentFnt.Texture == null) {
			textureNames.Add(fntName);
			texturesToPack.Add(fntName, new TextureToPack(fntName, fntFile, TextureType.Font));
		} else {
			Bitmap bmp;
			using (var ms = new MemoryStream(TextureWorker.ReadTextureBlob(fntFile))) {
				bmp = new Bitmap(ms);
			}
			bmp.SetResolution(96.0F, 96.0F);
			
			if ((bmp.Width == currentFnt.Texture.BoundingWidth && bmp.Height == currentFnt.Texture.BoundingHeight)) {
				currentFnt.Texture.ReplaceTexture((Image)bmp);
			} else {
				if (forceMatchingSpriteSize) {
					throw new Exception(String.Format("ERROR: forceMatchingSpriteSize has been configured to be true, but font {0} has a non-matching sprite size!", fntFile));
				}
				if (bmp.Width <= currentFnt.Texture.BoundingWidth && bmp.Height <= currentFnt.Texture.BoundingHeight) {
					Bitmap destBmp = new Bitmap((int)currentFnt.Texture.BoundingWidth, (int)currentFnt.Texture.BoundingHeight);
					destBmp.SetResolution(96.0F, 96.0F);
					using (Graphics grD = Graphics.FromImage(destBmp)) {
						grD.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
					}
					bmp = destBmp;
					currentFnt.Texture.ReplaceTexture((Image)bmp);
				} else if (bmp.Width > currentFnt.Texture.BoundingWidth || bmp.Height > currentFnt.Texture.BoundingHeight) {
					textureNames.Add(fntName);
					texturesToPack.Add(fntName, new TextureToPack(fntName, fntFile, TextureType.Font));
				}
			}
		}
	}
	
	// Handle packing
	// First set up packing directory
	string basePath = Path.Join("SpritePackager_Hg");
	string tempPath = Path.Join(basePath, "Temp");
	string outPath = Path.Join(basePath, "Output");
	
	System.IO.Directory.CreateDirectory(basePath);
	System.IO.Directory.CreateDirectory(tempPath);
	System.IO.Directory.CreateDirectory(outPath);
	
	// Next copy files
	
	foreach (string texName in textureNames) {
		TextureToPack texToPack = texturesToPack[texName];
		string sourceFile = texToPack.Path;
        string destinationFile = Path.Join(tempPath, Path.GetFileName(texToPack.Path));
		System.IO.File.Copy(sourceFile, destinationFile, true);
	}
	
	// Then run packer

	string searchPattern = "*.png";
	string outName = Path.Join(outPath, "atlas.txt");
	int textureSize = 2048;
	int PaddingValue = 2;
	bool debug = false;
	Packer packer = new Packer();
	packer.Process(tempPath, searchPattern, textureSize, PaddingValue, debug);
	packer.SaveAtlasses(outName);
	
	// After that, create TexturePageEntries and link them to their appropriate resources
	string prefix = outName.Replace(Path.GetExtension(outName), "");
	int atlasCount = 0;
	foreach (Atlas atlas in packer.Atlasses) {
		string atlasName = String.Format(prefix + "{0:000}" + ".png", atlasCount);
		Bitmap atlasBitmap = new Bitmap(atlasName);
		UndertaleEmbeddedTexture texture = new UndertaleEmbeddedTexture();
		texture.TextureData.TextureBlob = File.ReadAllBytes(atlasName);
		Data.EmbeddedTextures.Add(texture);
		foreach (Node n in atlas.Nodes) {
			if (n.Texture != null) {
				// Initalize values of this texture
				UndertaleTexturePageItem texturePageItem = new UndertaleTexturePageItem();
				texturePageItem.SourceX = (ushort)n.Bounds.X;
				texturePageItem.SourceY = (ushort)n.Bounds.Y;
				texturePageItem.SourceWidth = (ushort)n.Bounds.Width;
				texturePageItem.SourceHeight = (ushort)n.Bounds.Height;
				texturePageItem.TargetX = 0;
				texturePageItem.TargetY = 0;
				texturePageItem.TargetWidth = (ushort)n.Bounds.Width;
				texturePageItem.TargetHeight = (ushort)n.Bounds.Height;
				texturePageItem.BoundingWidth = (ushort)n.Bounds.Width;
				texturePageItem.BoundingHeight = (ushort)n.Bounds.Height;
				texturePageItem.TexturePage = texture;

				// Add this texture to UMT
				Data.TexturePageItems.Add(texturePageItem);

				// String processing
				string stripped = Path.GetFileNameWithoutExtension(n.Texture.Source);
				
				int lastUnderscore = -1;
				string spriteName = null;
				int frameNumber = -1;
				
				if (stripped.Contains('_')) {
					lastUnderscore = stripped.LastIndexOf('_');
					spriteName = stripped.Substring(0, lastUnderscore);
					frameNumber = Int32.Parse(stripped.Substring(lastUnderscore + 1));
				}
				
				TextureType texType;
				// Get our texture information to decide what kind of texture this is
				if (texturesToPack.ContainsKey(stripped)) {
					texType = texturesToPack[stripped].Type;
				} else if (texturesToPack.ContainsKey(spriteName)) {
					texType = texturesToPack[spriteName].Type;
				} else {
					throw new Exception(String.Format("ERROR: texture with filename {0} could not be matched to an asset.", n.Texture.Source));
				}
				
				// Create TextureEntry and link it to appropriate resource
				switch (texType) {
					case TextureType.Sprite:
						UndertaleSprite sprite = Data.Sprites.ByName(spriteName);
						UndertaleSprite.TextureEntry texEntry = new UndertaleSprite.TextureEntry();
						texEntry.Texture = texturePageItem;

						// Make default collision mask
						// if (sprite.CollisionMasks.Count <= 0 || sprite.CollisionMasks[0] == null) {
						// 	sprite.CollisionMasks.Add(sprite.NewMaskEntry());
						// 	Rectangle bmpRect = new Rectangle(n.Bounds.X, n.Bounds.Y, n.Bounds.Width, n.Bounds.Height);
						// 	System.Drawing.Imaging.PixelFormat format = atlasBitmap.PixelFormat;
						// 	Bitmap cloneBitmap = atlasBitmap.Clone(bmpRect, format);
						// 	int width = ((n.Bounds.Width + 7) / 8) * 8;
						// 	BitArray maskingBitArray = new BitArray(width * n.Bounds.Height);
						// 	for (int y = 0; y < n.Bounds.Height; y++) {
						// 		for (int x = 0; x < n.Bounds.Width; x++)
						// 		{
						// 			Color pixelColor = cloneBitmap.GetPixel(x, y);
						// 			maskingBitArray[y * width + x] = (pixelColor.A > 0);
						// 		}
						// 	}
						// 	BitArray tempBitArray = new BitArray(width * n.Bounds.Height);
						// 	for (int i = 0; i < maskingBitArray.Length; i += 8) {
						// 		for (int j = 0; j < 8; j++)
						// 		{
						// 			tempBitArray[j + i] = maskingBitArray[-(j - 7) + i];
						// 		}
						// 	}
						// 	int numBytes;
						// 	numBytes = maskingBitArray.Length / 8;
						// 	byte[] bytes = new byte[numBytes];
						// 	tempBitArray.CopyTo(bytes, 0);
						// 	for (int i = 0; i < bytes.Length; i++) {
						// 		sprite.CollisionMasks[0].Data[i] = bytes[i];
						// 	}
						// }

						sprite.Textures[frameNumber] = texEntry;
						break;
					case TextureType.Background:
						UndertaleBackground bg = Data.Backgrounds.ByName(stripped);
						bg.Texture = texturePageItem;
						break;
					case TextureType.Font:
						UndertaleFont font = Data.Fonts.ByName(stripped);
						font.Texture = texturePageItem;
						break;
					default:
						break;
				}
			}
		}
		// Increment atlas
		atlasCount++;
	}
	// Finally clean up files
}

class BackgroundInfo {
	public string Name;
	public bool Transparent = false;
	public bool Smooth = false;
	public bool Preload = false;
	public uint TileWidth = 32;
	public uint TileHeight = 32;
	public uint OutputBorderX = 2;
	public uint OutputBorderY = 2;
	public uint TileColumns = 32;
	public uint TileCount = 1024;
	public long FrameLength = 66666;
	
	public BackgroundInfo(string name) {
		this.Name = name;
	}
}

void ImportBackgroundInfo(string bgInfoPath, bool forceMatchingSpriteSize) {
	string bgPath = Path.GetDirectoryName(bgInfoPath);
	string currentBgName = null;
	BackgroundInfo currentBgInfo = null;
	
	// Import data from bgInfo.txt first.
	if (File.Exists(bgInfoPath)) {
		foreach (string lineIn in System.IO.File.ReadLines(bgInfoPath)) {
			string line = StripOpeningWhitespace(lineIn);
			if (line.Length < 1) {
				continue;
			}
			// Ignore comments
			if (line[0] == '#') {
				continue;
			}
			// Handle bg declarations
			if (line.Contains('{')) {
				currentBgName = StripClosingWhitespace(line.Split("{")[0]);
				currentBgInfo = new BackgroundInfo(currentBgName);
				// Override defaults if bg already exists
				UndertaleBackground bg = Data.Backgrounds.ByName(currentBgName);
				if (bg != null) {               
					currentBgInfo.Transparent   = bg.Transparent;
					currentBgInfo.Smooth        = bg.Smooth;
					currentBgInfo.Preload       = bg.Preload;
					currentBgInfo.TileWidth     = bg.GMS2TileWidth;
					currentBgInfo.TileHeight    = bg.GMS2TileHeight;
					currentBgInfo.OutputBorderX = bg.GMS2OutputBorderX;
					currentBgInfo.OutputBorderY = bg.GMS2OutputBorderY;
					currentBgInfo.TileColumns   = bg.GMS2TileColumns;
					currentBgInfo.TileCount     = bg.GMS2TileCount;
					currentBgInfo.FrameLength   = bg.GMS2FrameLength;
				}
				continue;
			// Handle bg definition end
			} else if (line.Contains('}')) {
				UndertaleBackground bg = Data.Backgrounds.ByName(currentBgName);
				if (bg == null) {
					UndertaleString bgUTString = Data.Strings.MakeString(currentBgName);
					bg = new UndertaleBackground();
					bg.Name = bgUTString;
					Data.Backgrounds.Add(bg);
				}
				bg.Transparent       = currentBgInfo.Transparent;
				bg.Smooth            = currentBgInfo.Smooth;
				bg.Preload           = currentBgInfo.Preload;
				bg.GMS2TileWidth     = currentBgInfo.TileWidth;
				bg.GMS2TileHeight    = currentBgInfo.TileHeight;
				bg.GMS2OutputBorderX = currentBgInfo.OutputBorderX;
				bg.GMS2OutputBorderY = currentBgInfo.OutputBorderY;
				bg.GMS2TileColumns   = currentBgInfo.TileColumns;
				bg.GMS2TileCount     = currentBgInfo.TileCount;
				bg.GMS2FrameLength   = currentBgInfo.FrameLength;
				continue;
			// Handle bg parameters
			} else if (line.Contains(':')) {
				string paramName = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[0]));
				string paramVal = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[1]));
				
				if (paramName.Equals("transparent")) {
					currentBgInfo.Transparent = Boolean.Parse(paramVal);
				} else if (paramName.Equals("smooth")) {
					currentBgInfo.Smooth = Boolean.Parse(paramVal);
				} else if (paramName.Equals("preload")) {
					currentBgInfo.Preload = Boolean.Parse(paramVal);
				} else if (paramName.Equals("tile_width")) {
					currentBgInfo.TileWidth = UInt32.Parse(paramVal);
				} else if (paramName.Equals("tile_height")) {
					currentBgInfo.TileHeight = UInt32.Parse(paramVal);
				} else if (paramName.Equals("output_border_x")) {
					currentBgInfo.OutputBorderX = UInt32.Parse(paramVal);
				} else if (paramName.Equals("output_border_y")) {
					currentBgInfo.OutputBorderY = UInt32.Parse(paramVal);
				} else if (paramName.Equals("tile_columns")) {
					currentBgInfo.TileColumns = UInt32.Parse(paramVal);
				} else if (paramName.Equals("tile_count")) {
					currentBgInfo.TileCount = UInt32.Parse(paramVal);
				} else if (paramName.Equals("frame_time")) {
					currentBgInfo.FrameLength= Int64.Parse(paramVal);
				} else {
					throw new Exception(String.Format("ERROR: Parameter '{0}' was declared, but '{0}' is not a valid bg parameter.", paramName));
				}
				continue;
			// Ignore empty lines
			} else {
				continue;
			}
		}
	}
	
	// Create bg data for any image files in the directory
	string[] bgFiles = Directory.GetFiles(bgPath, "*.png");
	foreach (string bgFile in bgFiles) {
		string bgName = Path.GetFileNameWithoutExtension(bgFile);
		UndertaleBackground bg = Data.Backgrounds.ByName(bgName);
		
		// Create a bg in the Data with default parameters if one does not yet exist.
		if (bg == null) {
			BackgroundInfo bgInfo = new BackgroundInfo(bgName);
			bg = new UndertaleBackground();
			UndertaleString bgUTString = new UndertaleString(bgName);
			
			bg.Name 		     = bgUTString;
			bg.Transparent       = bgInfo.Transparent;
			bg.Smooth            = bgInfo.Smooth;
			bg.Preload           = bgInfo.Preload;
			bg.GMS2TileWidth     = bgInfo.TileWidth;
			bg.GMS2TileHeight    = bgInfo.TileHeight;
			bg.GMS2OutputBorderX = bgInfo.OutputBorderX;
			bg.GMS2OutputBorderY = bgInfo.OutputBorderY;
			bg.GMS2TileColumns   = bgInfo.TileColumns;
			bg.GMS2TileCount     = bgInfo.TileCount;
			bg.GMS2FrameLength   = bgInfo.FrameLength;
			
			Data.Backgrounds.Add(bg);
		}
	}
}

class FontInfo {
	public string Name;
	public string DisplayName = "Arial";
	public uint FontSize = 12;
	public bool Bold = false;
	public bool Italic = false;
	public ushort RangeStart = 32;
	public uint RangeEnd = 127;
	public byte Charset = 1;
	public byte AntiAliasing = 1;
	public float ScaleX = 1;
	public float ScaleY = 1;
	
	public FontInfo(string name) {
		this.Name = name;
	}
}

UndertaleFont.Glyph GetFontGlyph(string[] argList) {
	UndertaleFont.Glyph returnGlyph = new UndertaleFont.Glyph();
	returnGlyph.Character    = UInt16.Parse(argList[0]);
	returnGlyph.SourceX      = UInt16.Parse(argList[1]);
	returnGlyph.SourceY      = UInt16.Parse(argList[2]);
	returnGlyph.SourceWidth  = UInt16.Parse(argList[3]);
	returnGlyph.SourceHeight = UInt16.Parse(argList[4]);
	returnGlyph.Shift        =  Int16.Parse(argList[5]);
	returnGlyph.Offset       =  Int16.Parse(argList[6]);
	return returnGlyph;
}

void ImportFontInfo(string fontInfoPath, bool forceMatchingSpriteSize) {
	string fontPath = Path.GetDirectoryName(fontInfoPath);
	string currentFontName = null;
	FontInfo currentFontInfo = null;
	
	// Import data from fntInfo.txt first.
	if (File.Exists(fontInfoPath)) {
		foreach (string lineIn in System.IO.File.ReadLines(fontInfoPath)) {
			string line = StripOpeningWhitespace(lineIn);
			if (line.Length < 1) {
				continue;
			}
			// Ignore comments
			if (line[0] == '#') {
				continue;
			}
			// Handle font declarations
			if (line.Contains('{')) {
				currentFontName = StripClosingWhitespace(line.Split("{")[0]);
				currentFontInfo = new FontInfo(currentFontName);
				// Override defaults if font already exists
				UndertaleFont font = Data.Fonts.ByName(currentFontName);
				if (font != null) {               
					currentFontInfo.DisplayName  = font.DisplayName.ToString();
					currentFontInfo.FontSize     = font.EmSize;
					currentFontInfo.Bold         = font.Bold;
					currentFontInfo.Italic       = font.Italic;
					currentFontInfo.RangeStart   = font.RangeStart;
					currentFontInfo.RangeEnd     = font.RangeEnd;
					currentFontInfo.Charset      = font.Charset;
					currentFontInfo.AntiAliasing = font.AntiAliasing;
					currentFontInfo.ScaleX       = font.ScaleX;
					currentFontInfo.ScaleY       = font.ScaleY;
				}
				continue;
			// Handle font definition end
			} else if (line.Contains('}')) {
				UndertaleFont font = Data.Fonts.ByName(currentFontName);
				if (font == null) {
					UndertaleString fontUTString = Data.Strings.MakeString(currentFontName);
					font = new UndertaleFont();
					font.Name = fontUTString;
					Data.Fonts.Add(font);
				}
				font.DisplayName  = Data.Strings.MakeString(currentFontInfo.DisplayName);
				font.EmSize       = currentFontInfo.FontSize;
				font.Bold         = currentFontInfo.Bold;
				font.Italic       = currentFontInfo.Italic;
				font.RangeStart   = currentFontInfo.RangeStart;
				font.RangeEnd     = currentFontInfo.RangeEnd;
				font.Charset      = currentFontInfo.Charset;
				font.AntiAliasing = currentFontInfo.AntiAliasing;
				font.ScaleX       = currentFontInfo.ScaleX;
				font.ScaleY       = currentFontInfo.ScaleY;
				continue;
			// Handle font parameters
			} else if (line.Contains(':')) {
				string paramName = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[0]));
				string paramVal = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[1]));
				
				if (paramName.Equals("display_name")) {
					currentFontInfo.DisplayName = paramVal;
				} else if (paramName.Equals("font_size")) {
					currentFontInfo.FontSize = UInt32.Parse(paramVal);
				} else if (paramName.Equals("bold")) {
					currentFontInfo.Bold = Boolean.Parse(paramVal);
				} else if (paramName.Equals("italic")) {
					currentFontInfo.Italic = Boolean.Parse(paramVal);
				} else if (paramName.Equals("range_start")) {
					currentFontInfo.RangeStart = UInt16.Parse(paramVal);
				} else if (paramName.Equals("range_end")) {
					currentFontInfo.RangeEnd = UInt32.Parse(paramVal);
				} else if (paramName.Equals("charset")) {
					currentFontInfo.Charset = Byte.Parse(paramVal);
				} else if (paramName.Equals("anti_aliasing")) {
					currentFontInfo.AntiAliasing = Byte.Parse(paramVal);
				} else if (paramName.Equals("scale_x")) {
					currentFontInfo.ScaleX = Single.Parse(paramVal);
				} else if (paramName.Equals("scale_y")) {
					currentFontInfo.ScaleY = Single.Parse(paramVal);
				} else {
					throw new Exception(String.Format("ERROR: Parameter '{0}' was declared, but '{0}' is not a valid font parameter.", paramName));
				}
				continue;
			// Ignore empty lines
			} else {
				continue;
			}
		}
	}

	// Import font glyph data
	string[] fontGlyphFiles = Directory.GetFiles(fontPath, "*.csv");
	foreach (string fontFile in fontGlyphFiles) {
		string fontName = Path.GetFileNameWithoutExtension(fontFile);
		UndertaleFont font = Data.Fonts.ByName(fontName);
		
		// Create a font in the Data with default parameters if one does not yet exist.
		if (font == null) {
			FontInfo fontInfo = new FontInfo(fontName);
			font = new UndertaleFont();
			UndertaleString fontUTString = new UndertaleString(fontName);
			
			font.Name 		  = fontUTString;
			font.DisplayName  = Data.Strings.MakeString(fontInfo.DisplayName);
			font.EmSize       = fontInfo.FontSize;
			font.Bold         = fontInfo.Bold;
			font.Italic       = fontInfo.Italic;
			font.RangeStart   = fontInfo.RangeStart;
			font.RangeEnd     = fontInfo.RangeEnd;
			font.Charset      = fontInfo.Charset;
			font.AntiAliasing = fontInfo.AntiAliasing;
			font.ScaleX       = fontInfo.ScaleX;
			font.ScaleY       = fontInfo.ScaleY;
			
			Data.Fonts.Add(font);
		}
		
		int lastCharIndex = -1;
		font.Glyphs.Clear();
		foreach (string line in System.IO.File.ReadLines(fontInfoPath)) {
			string[] glyphArgs = line.Split(",");
			if (glyphArgs.Length != 7) {
				throw new Exception(String.Format("ERROR: Line '{0}' in font file {1} had {2} arguments - expected 7.", line, fontFile, glyphArgs.Length));
			}
			UndertaleFont.Glyph nextGlyph = new UndertaleFont.Glyph();
			if (lastCharIndex >= (int)nextGlyph.Character) {
				throw new Exception(String.Format("ERROR: Glyphs in {0} were listed out of order, or duplicate entries were found.", fontFile));
			}
			if (lastCharIndex == -1) {
				font.RangeStart = nextGlyph.Character;
			}
			lastCharIndex = nextGlyph.Character;
			font.Glyphs.Add(nextGlyph);
		}
		font.RangeEnd = (uint)lastCharIndex;
	}
	
	// Create empty fonts if images define fonts that don't already exist
	string[] fontImageFiles = Directory.GetFiles(fontPath, "*.png");
	foreach (string fontFile in fontImageFiles) {
		string fontName = Path.GetFileNameWithoutExtension(fontFile);
		UndertaleFont font = Data.Fonts.ByName(fontName);
		
		// Create a font in the Data with default parameters if one does not yet exist.
		if (font == null) {
			FontInfo fontInfo = new FontInfo(fontName);
			font = new UndertaleFont();
			UndertaleString fontUTString = new UndertaleString(fontName);
			
			font.Name 		  = fontUTString;
			font.DisplayName  = Data.Strings.MakeString(fontInfo.DisplayName);
			font.EmSize       = fontInfo.FontSize;
			font.Bold         = fontInfo.Bold;
			font.Italic       = fontInfo.Italic;
			font.RangeStart   = fontInfo.RangeStart;
			font.RangeEnd     = fontInfo.RangeEnd;
			font.Charset      = fontInfo.Charset;
			font.AntiAliasing = fontInfo.AntiAliasing;
			font.ScaleX       = fontInfo.ScaleX;
			font.ScaleY       = fontInfo.ScaleY;
			
			Data.Fonts.Add(font);
		}
	}
}

class PathInfo {
	public string Name;
	public bool Smooth = false;
	public bool Closed = false;
	public uint Precision = 4;
	
	public PathInfo(string name) {
		this.Name = name;
	}
}

// Note that paths being imported are not file paths
// Paths in GameMaker are literal paths, a series of points for some object to travel between in order.
void ImportPaths(string pathInfoPath) {
	string pathPath = Path.GetDirectoryName(pathInfoPath);
	string currentPathName = null;
	PathInfo currentPathInfo = null;
	
	// Import data from pathInfo.txt first.
	if (File.Exists(pathInfoPath)) {
		foreach (string lineIn in System.IO.File.ReadLines(pathInfoPath)) {
			string line = StripOpeningWhitespace(lineIn);
			if (line.Length < 1) {
				continue;
			}
			// Ignore comments
			if (line[0] == '#') {
				continue;
			}
			// Handle path declarations
			if (line.Contains('{')) {
				currentPathName = StripClosingWhitespace(line.Split("{")[0]);
				currentPathInfo = new PathInfo(currentPathName);
				// Override defaults if path already exists
				UndertalePath path = Data.Paths.ByName(currentPathName);
				if (path != null) {               
					currentPathInfo.Smooth    = path.IsSmooth;
					currentPathInfo.Closed    = path.IsClosed;
					currentPathInfo.Precision = path.Precision;
				}
				continue;
			// Handle path definition end
			} else if (line.Contains('}')) {
				UndertalePath path = Data.Paths.ByName(currentPathName);
				if (path == null) {
					UndertaleString pathUTString = Data.Strings.MakeString(currentPathName);
					path = new UndertalePath();
					path.Name = pathUTString;
					path.IsSmooth  = currentPathInfo.Smooth;
					path.IsClosed  = currentPathInfo.Closed;
					path.Precision = currentPathInfo.Precision;
					Data.Paths.Add(path);
				} else {
					path.IsSmooth  = currentPathInfo.Smooth;
					path.IsClosed  = currentPathInfo.Closed;
					path.Precision = currentPathInfo.Precision;
				}
				continue;
			// Handle path parameters
			} else if (line.Contains(':')) {
				string paramName = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[0]));
				string paramVal = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[1]));
				
				if (paramName.Equals("smooth")) {
					currentPathInfo.Smooth = Boolean.Parse(paramVal);
				} else if (paramName.Equals("closed")) {
					currentPathInfo.Closed = Boolean.Parse(paramVal);
				} else if (paramName.Equals("precision")) {
					currentPathInfo.Precision = UInt32.Parse(paramVal);
				} else {
					throw new Exception(String.Format("ERROR: Parameter '{0}' was declared, but '{0}' is not a valid path parameter.", paramName));
				}
				continue;
			// Ignore empty lines
			} else {
				continue;
			}
		}
	}

	// Import path points
	string[] pathFiles = Directory.GetFiles(pathPath, "*.csv");
	foreach (string pathFile in pathFiles) {
		string pathName = Path.GetFileNameWithoutExtension(pathFile);
		UndertalePath path = Data.Paths.ByName(pathName);
		
		// Create a path in the Data with default parameters if one does not yet exist.
		if (path == null) {
			PathInfo pathInfo = new PathInfo(pathName);
			path = new UndertalePath();
			UndertaleString pathUTString = new UndertaleString(pathName);
			
			path.Name = pathUTString;
			path.IsSmooth  = pathInfo.Smooth;
			path.IsClosed  = pathInfo.Closed;
			path.Precision = pathInfo.Precision;
			
			Data.Paths.Add(path);
		}
		
		path.Points.Clear();
		// Open and read file's points.
		foreach(string line in System.IO.File.ReadLines(pathFile)) {
			// Ignore lines without a comma.
			if (line.Contains(",")) {
				string[] pointValues = line.Split(",");
				if (pointValues.Length != 3) {
					throw new Exception(String.Format("ERROR: Line '{0}' could not be comma-separated into three values.", line));
				}
				
				// Strip whitespace
				int i = 0;
				foreach(string pointValue in pointValues) {
					pointValues[i] = StripOpeningWhitespace(StripClosingWhitespace(pointValue));
					i++;
				}
				
				// Interpret values
				UndertalePath.PathPoint point = new UndertalePath.PathPoint();
				point.X     = Single.Parse(pointValues[0]);
				point.Y     = Single.Parse(pointValues[1]);
				point.Speed = Single.Parse(pointValues[2]);
				path.Points.Add(point);
			}
		}
	}
}

void CreateHollowCode(string srcPath) {
    string[] srcFiles = Directory.GetFiles(srcPath, "*.gml");
    foreach (string srcFile in srcFiles) {
        string assetName = Path.GetFileNameWithoutExtension(srcFile);
        if (Data.Code.ByName(assetName) == null) {
            UndertaleCode hollowCode = new UndertaleCode();
            hollowCode.Name = new UndertaleString(assetName);
            Data.Code.Add(hollowCode);
        }
    }
}

void CreateHollowRooms(string roomPath) {
    string[] roomFiles = Directory.GetFiles(roomPath, "*.json");
    foreach (string roomFile in roomFiles) {
        string assetName = Path.GetFileNameWithoutExtension(roomFile);
        if (Data.Rooms.ByName(assetName) == null) {
            UndertaleRoom hollowRoom = new UndertaleRoom();
            hollowRoom.Name = new UndertaleString(assetName);
            Data.Rooms.Add(hollowRoom);
        }
    }
}

void CreateHollowGameObjects(string objPath) {
    string[] objFiles = Directory.GetFiles(objPath, "*.json");
    foreach (string objFile in objFiles) {
        string assetName = Path.GetFileNameWithoutExtension(objFile);
        if (Data.GameObjects.ByName(assetName) == null) {
            UndertaleGameObject hollowObj = new UndertaleGameObject();
            hollowObj.Name = new UndertaleString(assetName);
            Data.GameObjects.Add(hollowObj);
        }
    }
}

void ImportMasks(string maskPath) {
    string[] maskFiles = Directory.GetFiles(maskPath, "*.msk");
    foreach (string maskFile in maskFiles) {
        string imageName = Path.GetFileNameWithoutExtension(maskFile);
        int lastUnderscore = imageName.LastIndexOf('_');
        string spriteName = imageName.Substring(0, lastUnderscore);
        int frameNumber = -1;
        try {
            frameNumber = Int32.Parse(imageName.Substring(lastUnderscore + 1));
        } catch {
            throw new Exception(String.Format("ERROR: Mask file at {0} does not have a valid frame number!", maskFile));
        }
        
        UndertaleSprite spriteToMask = Data.Sprites.ByName(spriteName);
        if (spriteToMask == null) {
            throw new Exception(String.Format("ERROR: Mask file at {0} does not correspond to an existing sprite!", maskFile));
        }
        if (spriteToMask.CollisionMasks.Count - 1 > frameNumber) {
            int numMasksToAdd = (spriteToMask.CollisionMasks.Count - 1) - frameNumber;
            for (int i = 0; i < numMasksToAdd; i++) {
                spriteToMask.CollisionMasks.Add(new UndertaleSprite.MaskEntry());
            }
        }
        
        spriteToMask.CollisionMasks[frameNumber].Data = File.ReadAllBytes(maskFile);
        // TODO: Ensure all frames exist
        
        
    }
}

void ImportRooms(string roomPath) {
    string[] roomFiles = Directory.GetFiles(roomPath, "*.json");
    foreach (string roomFile in roomFiles) {
        ReadRoom(roomFile);
    }
}

void ImportGameObjects(string objPath) {
    string[] objFiles = Directory.GetFiles(objPath, "*.json");
    foreach (string objFile in objFiles) {
        ReadGameObject(objFile);
    }
}

class SoundInfo {
    public string Name;
    public UndertaleSound.AudioEntryFlags Flags = UndertaleSound.AudioEntryFlags.IsEmbedded;
    public string Type = null;
    public string File = null;
    public uint Effects = 0;
    public float Volume = 1;
    public bool Preload = true;
    public float Pitch = 0;
    public string AudioGroup = null;
    public string AudioFile = null;
    public int AudioID = -69420;
    public int GroupID = -69420;
    
    public SoundInfo(string name) {
        this.Name = name;
    }
}

class SoundSourceInfo {
	public string Name;
	public int OldGroupID = 0;
	public int NewGroupID = 0;
	
	public SoundSourceInfo(string name, int oldGroupID, int newGroupID) {
		this.Name       = name;
		this.OldGroupID = oldGroupID;
		this.NewGroupID = newGroupID;
	}
}

void ImportSounds(string sndPath) {
	string sndInfoPath = Path.Join(sndPath, "soundInfo.txt");
	string currentSoundName = null;
	SoundInfo currentSoundInfo = null;
	Dictionary<string, IList<UndertaleEmbeddedAudio>> audioGroups = new Dictionary<string, IList<UndertaleEmbeddedAudio>>();
	
	// Keep track of switches of which audio group sound comes from
	Dictionary<string, SoundSourceInfo> soundsWithChangedDataSource = new Dictionary<string, SoundSourceInfo>();
	
    // Import data from soundInfo.txt first.
	if (File.Exists(sndInfoPath)) {
		foreach (string lineIn in System.IO.File.ReadLines(sndInfoPath)) {
			string line = StripOpeningWhitespace(lineIn);
			if (line.Length < 1) {
				continue;
			}
			// Ignore comments
			if (line[0] == '#') {
				continue;
			}
			// Handle sound declarations
			if (line.Contains('{')) {
				currentSoundName = StripClosingWhitespace(line.Split("{")[0]);
				currentSoundInfo = new SoundInfo(currentSoundName);
				// Override defaults if sound already exists
				UndertaleSound sound = Data.Sounds.ByName(currentSoundName);
				if (sound != null) {               
					currentSoundInfo.Flags      = sound.Flags;
					
					currentSoundInfo.Effects    = sound.Effects;
					currentSoundInfo.Volume     = sound.Volume;
					currentSoundInfo.Preload    = sound.Preload;
					currentSoundInfo.Pitch      = sound.Pitch;
					
					currentSoundInfo.AudioID    = sound.AudioID;
					currentSoundInfo.GroupID    = sound.GroupID;
					
					currentSoundInfo.Type       = sound.Type == null ? null : sound.Type.Content;
					currentSoundInfo.File       = sound.File == null ? null : sound.File.Content;
					
					currentSoundInfo.AudioGroup = (sound.AudioGroup == null || sound.AudioGroup.Name == null) ? null : sound.AudioGroup.Name.Content;
					currentSoundInfo.AudioFile  = (sound.AudioFile == null || sound.AudioFile.Name == null) ? null : sound.AudioFile.Name.Content;
				}
				continue;
			// Handle sound definition end
			} else if (line.Contains('}')) {
				UndertaleSound sound = Data.Sounds.ByName(currentSoundName);
				if (currentSoundInfo.AudioID == -69420) {
					throw new Exception(String.Format("ERROR: Audio ID for sound {0} could not be found! Audio ID is a mandatory field for new sounds.", currentSoundName));
				}
				if (currentSoundInfo.GroupID == -69420) {
					throw new Exception(String.Format("ERROR: Group ID for sound {0} could not be found! Group ID is a mandatory field for new sounds.", currentSoundName));
				}
				if (sound == null) {
					UndertaleString soundUTString = Data.Strings.MakeString(currentSoundName);
					sound = new UndertaleSound();
					sound.Name       = soundUTString;
					sound.Flags      = currentSoundInfo.Flags;
					sound.Type       = Data.Strings.MakeString(currentSoundInfo.Type);
					sound.File       = Data.Strings.MakeString(currentSoundInfo.File);
					sound.Effects    = currentSoundInfo.Effects;
					sound.Volume     = currentSoundInfo.Volume;
					sound.Preload    = currentSoundInfo.Preload;
					sound.Pitch      = currentSoundInfo.Pitch;
					sound.AudioGroup = Data.AudioGroups.ByName(currentSoundInfo.AudioGroup);
					sound.AudioFile  = Data.EmbeddedAudio.ByName(currentSoundInfo.AudioFile);
					sound.AudioID    = currentSoundInfo.AudioID;
					sound.GroupID    = currentSoundInfo.GroupID;
					
					sound.Type       = currentSoundInfo.Type == null ? null : Data.Strings.MakeString(currentSoundInfo.Type);
					sound.File       = currentSoundInfo.File == null ? null : Data.Strings.MakeString(currentSoundInfo.File);
					sound.AudioGroup = currentSoundInfo.AudioGroup == null ? null : Data.AudioGroups.ByName(currentSoundInfo.AudioGroup);
					sound.AudioFile  = currentSoundInfo.AudioFile == null ? null : Data.EmbeddedAudio.ByName(currentSoundInfo.AudioFile);
					
					Data.Sounds.Add(sound);
					
					
				} else {
					if (sound.GroupID != currentSoundInfo.GroupID) {
						SoundSourceInfo srcInfo = new SoundSourceInfo(sound.Name.Content, sound.GroupID, currentSoundInfo.GroupID);
						soundsWithChangedDataSource.Add(sound.Name.Content, srcInfo);
					}
					sound.Flags                   = currentSoundInfo.Flags;
					sound.Effects                 = currentSoundInfo.Effects;
					sound.Volume                  = currentSoundInfo.Volume;
					sound.Preload                 = currentSoundInfo.Preload;
					sound.Pitch                   = currentSoundInfo.Pitch;
					sound.AudioID                 = currentSoundInfo.AudioID;
					sound.GroupID                 = currentSoundInfo.GroupID;
					
					if (sound.Type == null) {
						sound.Type = currentSoundInfo.Type == null ? null : Data.Strings.MakeString(currentSoundInfo.Type);
					} else {
						sound.Type.Content = currentSoundInfo.Type == null ? null : currentSoundInfo.Type;
					}
					
					if (sound.File == null) {
						sound.File = currentSoundInfo.File == null ? null : Data.Strings.MakeString(currentSoundInfo.File);
					} else {
						sound.File.Content = currentSoundInfo.File == null ? null : currentSoundInfo.File;
					}
					
					sound.AudioGroup = currentSoundInfo.AudioGroup == null ? null : Data.AudioGroups.ByName(currentSoundInfo.AudioGroup);
					sound.AudioFile  = currentSoundInfo.AudioFile  == null ? null : Data.EmbeddedAudio.ByName(currentSoundInfo.AudioFile);
				}
				continue;
			// Handle sound parameters
			} else if (line.Contains(':')) {
				string paramName = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[0]));
				string paramVal = StripClosingWhitespace(StripOpeningWhitespace(line.Split(":")[1]));
				
				if (paramName.Equals("flags")) {
					currentSoundInfo.Flags = (UndertaleSound.AudioEntryFlags) Enum.Parse(typeof(UndertaleSound.AudioEntryFlags), paramVal, true);
				} else if (paramName.Equals("type")) {
					currentSoundInfo.Type = paramVal;
				} else if (paramName.Equals("file")) {
					currentSoundInfo.File = paramVal;
				} else if (paramName.Equals("effects")) {
					currentSoundInfo.Effects = UInt32.Parse(paramVal);
				} else if (paramName.Equals("volume")) {
					currentSoundInfo.Volume = Single.Parse(paramVal);
				} else if (paramName.Equals("preload")) {
					currentSoundInfo.Preload = Boolean.Parse(paramVal);
				} else if (paramName.Equals("pitch")) {
					currentSoundInfo.Pitch = Single.Parse(paramVal);
				} else if (paramName.Equals("audio_group")) {
					currentSoundInfo.AudioGroup = paramVal;
				} else if (paramName.Equals("audio_file")) {
					currentSoundInfo.AudioFile = paramVal;
				} else if (paramName.Equals("audio_id")) {
					currentSoundInfo.AudioID = Int32.Parse(paramVal);
				} else if (paramName.Equals("group_id")) {
					currentSoundInfo.GroupID = Int32.Parse(paramVal);
				} else {
					throw new Exception(String.Format("ERROR: Parameter '{0}' was declared, but '{0}' is not a valid sound parameter.", paramName));
				}
				continue;
			// Ignore empty lines
			} else {
				continue;
			}
		}
	}
	// Import audio from snd files
	string[] soundFiles = Directory.GetFiles(sndPath, "*.snd");
	foreach (string soundFile in soundFiles) {
		byte[] sndData = File.ReadAllBytes(soundFile);
		string sndName = Path.GetFileNameWithoutExtension(soundFile);
		UndertaleSound soundInData = Data.Sounds.ByName(sndName);
		if (soundInData == null) {
			throw new Exception(String.Format("New audio from file {0} was provided, but no associated entry in soundInfo.txt could be found", sndName));
		} else {
			if (soundInData.GroupID > Data.GetBuiltinSoundGroupID()) {
				string audioDataFilePath = Path.Combine(String.Format("{0}{1}audiogroup{2}.dat", Path.GetDirectoryName(FilePath), Path.DirectorySeparatorChar, soundInData.GroupID));
				FileStream audioGroupReadStream = new FileStream(audioDataFilePath, FileMode.Open, FileAccess.Read);
				UndertaleData audioGroupData = UndertaleIO.Read(audioGroupReadStream);
				audioGroupReadStream.Dispose();
				
				
				if (soundInData.AudioID < 0) {
					throw new Exception(String.Format("ERROR: Cannot use Audio ID with value less than 1! Value of {0} used for Audio ID of sound {1}.", soundInData.AudioID, soundInData.Name.Content));
				}
				
				UndertaleEmbeddedAudio embeddedAudio = null;
				if (audioGroupData.EmbeddedAudio.Count - 1 < soundInData.AudioID) {
					embeddedAudio = new UndertaleEmbeddedAudio();
					embeddedAudio.Name = new UndertaleString(String.Format("EmbeddedSound {0} (UndertaleEmbeddedAudio)", soundInData.AudioID));
					embeddedAudio.Data = sndData;
					while (audioGroupData.EmbeddedAudio.Count < soundInData.AudioID) {
						audioGroupData.EmbeddedAudio.Add(new UndertaleEmbeddedAudio());
					}
					audioGroupData.EmbeddedAudio.Add(embeddedAudio);
				} else {
					embeddedAudio = audioGroupData.EmbeddedAudio[soundInData.AudioID];
					embeddedAudio.Data = sndData;
				}
				
				if (embeddedAudio == null) {
					throw new Exception(String.Format("ERROR: Could not find Embedded Audio of sound {0}.", soundInData.Name.Content));
				}
				
				
				FileStream audioGroupWriteStream = new FileStream(audioDataFilePath, FileMode.Create);
				UndertaleIO.Write(audioGroupWriteStream, audioGroupData); // Write it to the disk
				audioGroupWriteStream.Dispose();
				
				
			} else {
				if (soundInData.AudioFile == null) {
					UndertaleEmbeddedAudio newAudioFile = new UndertaleEmbeddedAudio();
					newAudioFile.Name = new UndertaleString(String.Format("EmbeddedSound {0} (UndertaleEmbeddedAudio)", soundInData.AudioID));
					Data.EmbeddedAudio.Add(newAudioFile);
					soundInData.AudioFile = newAudioFile;
				}
				soundInData.AudioFile.Data = sndData;
			}
		}
	}
}

// I'm sure there's a better way to do this,
// But I can't be fucked.
bool ReadCfgBool(string cfgPath, string paramName) {
	// Also include the / since a val might be immediately followed by a comment
	foreach (string lineIn in System.IO.File.ReadLines(cfgPath)) {
		string line = StripOpeningWhitespace(lineIn);
		if (line.Contains(paramName)) {
			string paramVal = line.Split(paramName + " ")[1];
			// Isolate to just a single word - cut off anything after a whitespace.
			paramVal = StripClosingWhitespace(paramVal);
			try {
				return Boolean.Parse(paramVal);
			} catch (FormatException ex) {
				throw new FormatException(String.Format("ERROR: Config variable {0} was declared, but could not be parsed into a boolean.\nParam value found: ", paramName, paramVal));
			}
		}
	}
	throw new Exception(String.Format("ERROR: Mandatory boolean config variable {0} was never declared.", paramName));
}

PatchVersionInfo ReadCfgVersion(string cfgPath, string paramName) {
	// Also include the / since a val might be immediately followed by a comment
	foreach (string lineIn in System.IO.File.ReadLines(cfgPath)) {
		string line = StripOpeningWhitespace(lineIn);
		if (line.Contains(paramName)) {
			string paramVal = line.Split(paramName + " ")[1];
			// Isolate to just a single word - cut off anything after a whitespace.
			paramVal = StripClosingWhitespace(paramVal);
			try {
				return new PatchVersionInfo(paramVal);
			} catch (FormatException ex) {
				throw new FormatException(String.Format("ERROR: Config variable {0} was declared, but could not be parsed into Version Info.\nParam value found: ", paramName, paramVal));
			}
		}
	}
	throw new Exception(String.Format("ERROR: Mandatory Version Info config variable {0} was never declared.", paramName));
}

class PatchVersionInfo {
	public int MajorVersion; // Changes to format itself
	public int MinorVersion; // Changes to script versions primarily, ex. bugfixes and stuff.
	
	public PatchVersionInfo(string Version) {
		string[] subversions = Version.Split('.');
		this.MajorVersion = Int32.Parse(subversions[0]);
		this.MinorVersion = Int32.Parse(subversions[1]);
	}
}


///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY //
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

EnsureDataLoaded();

// Get patch folder
string patchPath = PromptChooseDirectory("Locate your uncompressed HgPatch directory.");

// Throw error if folder is not provided.
if (patchPath == null) {
	throw new System.Exception("ERROR: No patch folder was provided.");
}

// Construct paths
string cfgPath        = Path.Join(patchPath, "patch.cfg");
string srcPath        = Path.Join(patchPath, "src");
string spritePath     = Path.Join(patchPath, "spr");
string spriteInfoPath = Path.Join(spritePath, "spriteInfo.txt");
string bgPath         = Path.Join(patchPath, "bg");
string bgInfoPath     = Path.Join(bgPath, "bgInfo.txt");
string maskPath       = Path.Join(patchPath, "mask");
string soundPath      = Path.Join(patchPath, "snd");
string roomPath       = Path.Join(patchPath, "room");
string objPath        = Path.Join(patchPath, "obj");
string fntPath        = Path.Join(patchPath, "fnt");
string fntInfoPath    = Path.Join(fntPath, "fntInfo.txt");
string pathPath       = Path.Join(patchPath, "path"); // I'm sure this name will cause no confusion whatsoever.
string pathInfoPath   = Path.Join(pathPath, "pathInfo.txt"); // I'm sure this name will cause no confusion whatsoever.

// Read each config variable we need
bool forceMatchingSpriteSize = ReadCfgBool(cfgPath, "force_matching_sprite_size");
PatchVersionInfo patchVersion = ReadCfgVersion(cfgPath, "patch_version");

// Get default Patch Version
PatchVersionInfo scriptVersion = new PatchVersionInfo("1.0");

bool continuePatch = true;
// Give warning if this script was made for an earlier patch version.
if (patchVersion.MajorVersion > scriptVersion.MajorVersion) {
	continuePatch = ScriptQuestion(String.Format("The Patcher script is {0} major versions behind the patch you are attempting to use.\nIt is highly recommended that you download the most up-to-date patcher version at https://github.com/SolventMercury/HgPatcher/releases before attempting to proceed.\nContinue anyways?", patchVersion.MajorVersion - scriptVersion.MajorVersion));
}

if (!continuePatch) {
	ScriptMessage("Aborted patch application...");
	return;
}

// TODO:

// Register Deletions
// RegisterEntityDeletions();

// Create empty game objects with only basic details
// It's important to get this out of the way now, as these assets may be referenced by future code,
// But we can't create them fully as they may also require other assets.
CreateHollowGameObjects(objPath);

// Do the same for rooms
CreateHollowRooms(roomPath);

// And also for code
CreateHollowCode(srcPath);

// Next import assets

// First import some basic ones.
// Import Paths
ImportPaths(pathInfoPath);

// Import sounds
ImportSounds(soundPath);

// Now for sprites, backgrounds, fonts, textures, and masks.
// Delightful(ly complex)!

// Set default values for sVersion if this game has no sprites
// I don't know much about sVersion, but I think it'll be the same for all sprites in a game.

int sVersion;

if (Data.GeneralInfo?.Major >= 2) {
	sVersion = 2;
} else {
	sVersion = 1;
}
if (!((Data.GMS2_3 == false) && (Data.GMS2_3_1 == false) && (Data.GMS2_3_2 == false))) {
	sVersion = 3;
}

// Get sVersion directly off of a sprite if we can
if (Data.Sprites.Count > 0) {
	sVersion = (int)Data.Sprites[0].SVersion;
}

// Apply data from spriteInfo.txt
// Returns a dict linking each new/modified sprite's name to its sprite info
Dictionary<string, SpriteInfo> spriteInfoDict = ImportSpriteInfo(spriteInfoPath, forceMatchingSpriteSize, sVersion);

// Import font and background data
// Also create their entries in UMT if they don't already exist
ImportBackgroundInfo(bgInfoPath, forceMatchingSpriteSize);
ImportFontInfo(fntInfoPath, forceMatchingSpriteSize);

// Import textures
ImportTextures(spritePath, bgPath, fntPath, spriteInfoDict, forceMatchingSpriteSize);

// Import masks
ImportMasks(maskPath);

// Now rearrange order of all assets I think?
// Need to research that

// Finally, import code!
BasicCodeImport(srcPath);
//if (!((Data.GMS2_3 == false) && (Data.GMS2_3_1 == false) && (Data.GMS2_3_2 == false))) {
//    AdvancedCodeImport(srcPath);
//} else {
//    BasicCodeImport(srcPath);
//}

// Import GameObjects
ImportGameObjects(objPath);

// Import Rooms
ImportRooms(roomPath);

ScriptMessage("Finished importing patch! Be sure to save your data.win before you close.");