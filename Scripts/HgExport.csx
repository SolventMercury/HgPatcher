// HgPatcher - a universal patching format for GML.
// This script is used to generate a patch from two data.win files.
// The patch is just a list of differences between the vanilla and modified data.win files.
// No file needs to be open in UMT for this script to work.
// NOTE: For games with external audiogroup files, this script will automatically search the folder that contains each data.win.
// As such, if you've actually made changes to audiogroup files, you need to make sure each data.win is in its own folder,
// And each one needs to come with its own copies of the appropriate audogroup files.
// Made by https://github.com/SolventMercury with contributions from Jockeholm

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UndertaleModLib;
using UndertaleModLib.Util;
using UndertaleModLib.Models;
using UndertaleModLib.Scripting;
using UndertaleModLib.Decompiler;
using ImageMagick;

List<string> CodeThatCouldNotBeDecompiled = new List<string>();
TextureWorker worker = new TextureWorker();
ThreadLocal<GlobalDecompileContext> DECOMPILE_CONTEXT = new ThreadLocal<GlobalDecompileContext>(() => new GlobalDecompileContext(Data, false));
int numSteps = 5;

enum AssetType {
	Sound,
	Path,
	Code,
	Sprite,
	Background,
	Font,
	Room,
	Shader,
	Extension,
	Timeline,
	GameObject,
	Mask
}



// Borrowed method from the import script I wrote
void WriteString(Utf8JsonWriter writer, string propertyName, UndertaleString stringToWrite) {
	if (stringToWrite == null) {
		writer.WriteNull(propertyName);
	} else {
		if (stringToWrite.Content == null) {
			writer.WriteNull(propertyName);
		} else {
			writer.WriteString(propertyName, stringToWrite.Content);
		}
	}
}

string RoomToJson(UndertaleRoom room) {
	JsonWriterOptions writerOptions = new() {Indented = true};
	using MemoryStream stream = new();
	using Utf8JsonWriter writer = new(stream, writerOptions);
	writer.WriteStartObject();
	// Params
	//{
	WriteString(writer, "name", room.Name);
	WriteString(writer, "caption", room.Caption);
	writer.WriteNumber("width", room.Width);
	writer.WriteNumber("height", room.Height);
	writer.WriteNumber("speed", room.Speed);
	writer.WriteBoolean("persistent", room.Persistent);
	writer.WriteNumber("background_color", room.BackgroundColor);
	writer.WriteBoolean("draw_background_color", room.DrawBackgroundColor);
	//}
	
	// GMS 2 Params
	//{
	if (room.CreationCodeId != null) {
		WriteString(writer, "creation_code_id", room.CreationCodeId.Name);
	} else {
		writer.WriteNull("creation_code_id");
	}

	writer.WriteNumber("flags", Convert.ToInt32(room.Flags));
	writer.WriteBoolean("world", room.World);
	writer.WriteNumber("top", room.Top);
	writer.WriteNumber("left", room.Left);
	writer.WriteNumber("right", room.Right);
	writer.WriteNumber("bottom", room.Bottom);
	writer.WriteNumber("gravity_x", room.GravityX);
	writer.WriteNumber("gravity_y", room.GravityY);
	writer.WriteNumber("meters_per_pixel", room.MetersPerPixel);
	
	//}
	
	// Now the part that sucks
	
	// Backgrounds
	writer.WriteStartArray("backgrounds");
	if (room.Backgrounds != null) {
		foreach (UndertaleRoom.Background bg in room.Backgrounds) {
			writer.WriteStartObject();
			if (bg != null) {
				writer.WriteNumber("calc_scale_x", bg.CalcScaleX);
				writer.WriteNumber("calc_scale_y", bg.CalcScaleY);
				writer.WriteBoolean("enabled", bg.Enabled);
				writer.WriteBoolean("foreground", bg.Foreground);

				if (bg.BackgroundDefinition != null) {
					WriteString(writer, "background_definition", bg.BackgroundDefinition.Name);
				} else {
					writer.WriteNull("background_definition");
				}
				
				writer.WriteNumber("x", bg.X);
				writer.WriteNumber("y", bg.Y);
				writer.WriteNumber("speed_x", bg.SpeedX);
				writer.WriteNumber("speed_y", bg.SpeedY);

			}
			
			writer.WriteEndObject();
		}
	}
	writer.WriteEndArray();

	// Views
	writer.WriteStartArray("views");
	if (room.Views != null) {
		foreach (UndertaleRoom.View view in room.Views) {
			writer.WriteStartObject();
			
			if (view != null) {
				writer.WriteBoolean("enabled", view.Enabled);
				writer.WriteNumber("view_x", view.ViewX);
				writer.WriteNumber("view_y", view.ViewY);
				writer.WriteNumber("view_width", view.ViewWidth);
				writer.WriteNumber("view_height", view.ViewHeight);
				writer.WriteNumber("port_x", view.PortX);
				writer.WriteNumber("port_y", view.PortY);
				writer.WriteNumber("port_width", view.PortWidth);
				writer.WriteNumber("port_height", view.PortHeight);
				writer.WriteNumber("border_x", view.BorderX);
				writer.WriteNumber("border_y", view.BorderY);
				writer.WriteNumber("speed_x", view.SpeedX);
				writer.WriteNumber("speed_y", view.SpeedY);
				
				if (view.ObjectId != null) {
					WriteString(writer, "object_id", view.ObjectId.Name);
				} else {
					writer.WriteNull("object_id");
				}
			}
			
			writer.WriteEndObject();
		}
	}
	writer.WriteEndArray();
	
	// GameObjects
	writer.WriteStartArray("game_objects");
	if (room.GameObjects != null) {
		foreach (UndertaleRoom.GameObject go in room.GameObjects) {
			writer.WriteStartObject();
			if (go != null) {
				writer.WriteNumber("x", go.X);
				writer.WriteNumber("y", go.Y);

				if (go.ObjectDefinition != null) {
					WriteString(writer, "object_definition", go.ObjectDefinition.Name);
				} else {
					writer.WriteNull("object_definition");
				}

				writer.WriteNumber("instance_id", go.InstanceID);

				if (go.CreationCode != null) {
					WriteString(writer, "creation_code", go.CreationCode.Name);
				} else {
					writer.WriteNull("creation_code");
				}

				writer.WriteNumber("scale_x", go.ScaleX);
				writer.WriteNumber("scale_y", go.ScaleY);
				writer.WriteNumber("color", go.Color);
				writer.WriteNumber("rotation", go.Rotation);
				
				if (go.PreCreateCode != null) {
					WriteString(writer, "pre_create_code", go.PreCreateCode.Name);
				} else {
					writer.WriteNull("pre_create_code");
				}

				writer.WriteNumber("image_speed", go.ImageSpeed);
				writer.WriteNumber("image_index", go.ImageIndex);

			}
			
			writer.WriteEndObject();
		}
	}
	writer.WriteEndArray();
	
	// Tiles
	writer.WriteStartArray("tiles");
	if (room.Tiles != null) {
		foreach (UndertaleRoom.Tile tile in room.Tiles) {
			writer.WriteStartObject();
			if (tile != null) {
				//writer.WriteBoolean("sprite_mode", tile._SpriteMode);
				writer.WriteNumber("x", tile.X);
				writer.WriteNumber("y", tile.Y);

				if (tile.BackgroundDefinition != null) {
					WriteString(writer, "background_definition", tile.BackgroundDefinition.Name);
				} else {
					writer.WriteNull("background_definition");
				}

				if (tile.SpriteDefinition != null) {
					WriteString(writer, "sprite_definition", tile.SpriteDefinition.Name);
				} else {
					writer.WriteNull("sprite_definition");
				}

				writer.WriteNumber("source_x", tile.SourceX);
				writer.WriteNumber("source_y", tile.SourceY);
				writer.WriteNumber("width", tile.Width);
				writer.WriteNumber("height", tile.Height);
				writer.WriteNumber("tile_depth", tile.TileDepth);
				writer.WriteNumber("instance_id", tile.InstanceID);
				writer.WriteNumber("scale_x", tile.ScaleX);
				writer.WriteNumber("scale_y", tile.ScaleY);
				writer.WriteNumber("color", tile.Color);

			}
			
			writer.WriteEndObject();
		}
	}
	writer.WriteEndArray();
	
	// Layers
	// This is the part that super sucks
	
	writer.WriteStartArray("layers");
	if (room.Layers != null) {
		foreach (UndertaleRoom.Layer layer in room.Layers) {
			writer.WriteStartObject();
			if (layer != null) {
				//{
				WriteString(writer, "layer_name", layer.LayerName);
				writer.WriteNumber("layer_id", layer.LayerId);
				writer.WriteNumber("layer_type", Convert.ToInt32(layer.LayerType));
				writer.WriteNumber("layer_depth", layer.LayerDepth);
				writer.WriteNumber("x_offset", layer.XOffset);
				writer.WriteNumber("y_offset", layer.YOffset);
				writer.WriteNumber("h_speed", layer.HSpeed);
				writer.WriteNumber("v_speed", layer.VSpeed);
				writer.WriteBoolean("is_visible", layer.IsVisible);
				//}
				writer.WriteStartObject("layer_data");
				if (layer.Data != null) {
					if (layer.LayerType == UndertaleRoom.LayerType.Background) {
						UndertaleRoom.Layer.LayerBackgroundData layerData = (UndertaleRoom.Layer.LayerBackgroundData)layer.Data;

						writer.WriteNumber("calc_scale_x", layerData.CalcScaleX);
						writer.WriteNumber("calc_scale_y", layerData.CalcScaleY);
						writer.WriteBoolean("visible", layerData.Visible);
						writer.WriteBoolean("foreground", layerData.Foreground);
						
						if (layerData.Sprite != null) {
							WriteString(writer, "sprite", layerData.Sprite.Name);
						} else {
							writer.WriteNull("sprite");
						}

						writer.WriteBoolean("tiled_horizontally", layerData.TiledHorizontally);
						writer.WriteBoolean("tiled_vertically", layerData.TiledVertically);
						writer.WriteBoolean("stretch", layerData.Stretch);
						writer.WriteNumber("color", layerData.Color);
						writer.WriteNumber("first_frame", layerData.FirstFrame);
						writer.WriteNumber("animation_speed", layerData.AnimationSpeed);
						writer.WriteNumber("animation_speed_type", Convert.ToInt32(layerData.AnimationSpeedType));
					}
					
					if (layer.LayerType == UndertaleRoom.LayerType.Instances) {
						UndertaleRoom.Layer.LayerInstancesData layerData = (UndertaleRoom.Layer.LayerInstancesData)layer.Data;
						
						writer.WriteStartArray("instances");
						if (layerData.Instances != null) {
							foreach (UndertaleRoom.GameObject instance in layerData.Instances) {
								writer.WriteStartObject();
								if (instance != null) {
									writer.WriteNumber("x", instance.X);
									writer.WriteNumber("y", instance.Y);

									if (instance.ObjectDefinition != null) {
										WriteString(writer, "object_definition", instance.ObjectDefinition.Name);
									} else {
										writer.WriteNull("object_definition");
									}

									writer.WriteNumber("instance_id", instance.InstanceID);

									if (instance.CreationCode != null) {
										WriteString(writer, "creation_code", instance.CreationCode.Name);
									} else {
										writer.WriteNull("creation_code");
									}

									writer.WriteNumber("scale_x", instance.ScaleX);
									writer.WriteNumber("scale_y", instance.ScaleY);
									writer.WriteNumber("color", instance.Color);
									writer.WriteNumber("rotation", instance.Rotation);
									
									if (instance.PreCreateCode != null) {
										WriteString(writer, "pre_create_code", instance.PreCreateCode.Name);
									} else {
										writer.WriteNull("pre_create_code");
									}

									writer.WriteNumber("image_speed", instance.ImageSpeed);
									writer.WriteNumber("image_index", instance.ImageIndex);

								}
								
								writer.WriteEndObject();
							}
						}
						writer.WriteEndArray();
					}
					
					// Awful^3
					if (layer.LayerType == UndertaleRoom.LayerType.Assets) {
						UndertaleRoom.Layer.LayerAssetsData layerData = (UndertaleRoom.Layer.LayerAssetsData)layer.Data;
						// Tiles
						writer.WriteStartArray("legacy_tiles");
						if (layerData.LegacyTiles != null) {
							foreach (UndertaleRoom.Tile tile in layerData.LegacyTiles) {
								writer.WriteStartObject();
								if (tile != null) {
									//writer.WriteBoolean("sprite_mode", tile._SpriteMode);
									writer.WriteNumber("x", tile.X);
									writer.WriteNumber("y", tile.Y);
									
									if (tile.SpriteDefinition != null) {
										WriteString(writer, "background_definition", tile.SpriteDefinition.Name);
									} else {
										writer.WriteNull("background_definition");
									}

									if (tile.BackgroundDefinition != null) {
										WriteString(writer, "sprite_definition", tile.BackgroundDefinition.Name);
									} else {
										writer.WriteNull("sprite_definition");
									}

									writer.WriteNumber("source_x", tile.SourceX);
									writer.WriteNumber("source_y", tile.SourceY);
									writer.WriteNumber("width", tile.Width);
									writer.WriteNumber("height", tile.Height);
									writer.WriteNumber("tile_depth", tile.TileDepth);
									writer.WriteNumber("instance_id", tile.InstanceID);
									writer.WriteNumber("scale_x", tile.ScaleX);
									writer.WriteNumber("scale_y", tile.ScaleY);
									writer.WriteNumber("color", tile.Color);
								}
								writer.WriteEndObject();
							}
						}
						writer.WriteEndArray();
						
						// Sprites
						writer.WriteStartArray("sprites");
						if (layerData.Sprites != null) {
							foreach (UndertaleRoom.SpriteInstance sprite in layerData.Sprites) {
								writer.WriteStartObject();
								if (sprite != null) {
									WriteString(writer, "name", sprite.Name);
									
									if (sprite.Sprite != null) {
										WriteString(writer, "sprite", sprite.Sprite.Name);
									} else {
										writer.WriteNull("sprite");
									}

									writer.WriteNumber("x", sprite.X);
									writer.WriteNumber("y", sprite.Y);
									writer.WriteNumber("scale_x", sprite.ScaleX);
									writer.WriteNumber("scale_y", sprite.ScaleY);
									writer.WriteNumber("color", sprite.Color);
									writer.WriteNumber("animation_speed", sprite.AnimationSpeed);
									writer.WriteNumber("animation_speed_type", Convert.ToInt32(sprite.AnimationSpeedType));
									writer.WriteNumber("frame_index", sprite.FrameIndex);
									writer.WriteNumber("rotation", sprite.Rotation);

								}
								writer.WriteEndObject();
							}
						}
						writer.WriteEndArray();
						
						// Sequences
						writer.WriteStartArray("sequences");
						if (layerData.Sequences != null) {
							foreach (UndertaleRoom.SequenceInstance sequence in layerData.Sequences) {
								writer.WriteStartObject();
								if (sequence != null) {
									WriteString(writer, "name", sequence.Name);
									
									if (sequence.Sequence != null) {
										WriteString(writer, "sequence", sequence.Sequence.Name);
									} else {
										writer.WriteNull("sequence");
									}

									writer.WriteNumber("x", sequence.X);
									writer.WriteNumber("y", sequence.Y);
									writer.WriteNumber("scale_x", sequence.ScaleX);
									writer.WriteNumber("scale_y", sequence.ScaleY);
									writer.WriteNumber("color", sequence.Color);
									writer.WriteNumber("animation_speed", sequence.AnimationSpeed);
									writer.WriteNumber("animation_speed_type", Convert.ToInt32(sequence.AnimationSpeedType));
									writer.WriteNumber("frame_index", sequence.FrameIndex);
									writer.WriteNumber("rotation", sequence.Rotation);
								}
								writer.WriteEndObject();
							}
						}
						writer.WriteEndArray();
						
						// NineSlices
						writer.WriteStartArray("nine_slices");
						if (layerData.NineSlices != null) {
							foreach (UndertaleRoom.SpriteInstance nineSlice in layerData.NineSlices) {
								writer.WriteStartObject();
								if (nineSlice != null) {
									WriteString(writer, "name", nineSlice.Name);
									
									if (nineSlice.Sprite != null) {
										WriteString(writer, "sprite", nineSlice.Sprite.Name);
									} else {
										writer.WriteNull("sprite");
									}

									writer.WriteNumber("x", nineSlice.X);
									writer.WriteNumber("y", nineSlice.Y);
									writer.WriteNumber("scale_x", nineSlice.ScaleX);
									writer.WriteNumber("scale_y", nineSlice.ScaleY);
									writer.WriteNumber("color", nineSlice.Color);
									writer.WriteNumber("animation_speed", nineSlice.AnimationSpeed);
									writer.WriteNumber("animation_speed_type", Convert.ToInt32(nineSlice.AnimationSpeedType));
									writer.WriteNumber("frame_index", nineSlice.FrameIndex);
									writer.WriteNumber("rotation", nineSlice.Rotation);

								}
								writer.WriteEndObject();
							}
						}
						writer.WriteEndArray();
					}
					
					if (layer.LayerType == UndertaleRoom.LayerType.Tiles) {
						UndertaleRoom.Layer.LayerTilesData layerData = (UndertaleRoom.Layer.LayerTilesData)layer.Data;

						writer.WriteNumber("tiles_x", layerData.TilesX);
						writer.WriteNumber("tiles_y", layerData.TilesY);

						writer.WriteStartArray("tile_data");
						if (layerData.TileData != null) {
							for (int x = 0; x < layerData.TileData.Length; x++) {
								writer.WriteStartArray();
								for (int y = 0; y < layerData.TileData[x].Length; y++) {
									writer.WriteStartObject();
									writer.WriteNumber("id", (layerData.TileData[x])[y]);
									writer.WriteEndObject();
								}
								writer.WriteEndArray();
							}
						}
						writer.WriteEndArray();
					}
				}
				writer.WriteEndObject();
			}
			
			writer.WriteEndObject();
		}
	}
	writer.WriteEndArray();

	writer.WriteEndObject();
	writer.Flush();
	
	string json = Encoding.UTF8.GetString(stream.ToArray());
	return json;
}

string GameObjectToJson(UndertaleGameObject gameObject) {
	JsonWriterOptions writerOptions = new() {Indented = true};
	using MemoryStream stream = new();
	using Utf8JsonWriter writer = new(stream, writerOptions);
	writer.WriteStartObject();
	string json;
	if (gameObject == null) {
		writer.WriteEndObject();
		writer.Flush();
		json = Encoding.UTF8.GetString(stream.ToArray());
		return json;
	}
	
	WriteString(writer, "name", gameObject.Name);
	
	if (gameObject.Sprite != null) {
		WriteString(writer, "sprite", gameObject.Sprite.Name);
	} else {
		writer.WriteNull("sprite");
	}
	
	writer.WriteBoolean("visible", gameObject.Visible);
	writer.WriteBoolean("solid", gameObject.Solid);
	writer.WriteNumber("depth", gameObject.Depth);
	writer.WriteBoolean("persistent", gameObject.Persistent);
	
	if (gameObject.ParentId != null) {
		WriteString(writer, "parent_id", gameObject.ParentId.Name);
	} else {
		writer.WriteNull("parent_id");
	}
	
	if (gameObject.TextureMaskId != null) {
		WriteString(writer, "texture_mask_id", gameObject.TextureMaskId.Name);
	} else {
		writer.WriteNull("texture_mask_id");
	}
	
	writer.WriteBoolean("uses_physics", gameObject.UsesPhysics);
	writer.WriteBoolean("is_sensor", gameObject.IsSensor);
	writer.WriteNumber("collision_shape", Convert.ToInt32(gameObject.CollisionShape));
	writer.WriteNumber("density", gameObject.Density);
	writer.WriteNumber("restitution", gameObject.Restitution);
	writer.WriteNumber("group", gameObject.Group);
	writer.WriteNumber("linear_damping", gameObject.LinearDamping);
	writer.WriteNumber("angular_damping", gameObject.AngularDamping);
	writer.WriteNumber("friction", gameObject.Friction);
	writer.WriteBoolean("awake", gameObject.Awake);
	writer.WriteBoolean("kinematic", gameObject.Kinematic);
	
	writer.WriteStartArray("physics_vertices");
	if (gameObject.PhysicsVertices != null) {
		foreach(UndertaleGameObject.UndertalePhysicsVertex vertex in gameObject.PhysicsVertices) {
			writer.WriteStartObject();
			writer.WriteNumber("x", vertex.X);
			writer.WriteNumber("y", vertex.Y);
			writer.WriteEndObject();
		}
	}
	writer.WriteEndArray();
	
	writer.WriteStartArray("events");
	if (gameObject.Events != null) {
		foreach(IList<UndertaleGameObject.Event> eventList in gameObject.Events) {
			writer.WriteStartArray();
			if (eventList != null) {
				foreach(UndertaleGameObject.Event objectEvent in eventList) {
					writer.WriteStartObject();
					if (objectEvent != null) {
						writer.WriteNumber("event_subtype", objectEvent.EventSubtype);
						writer.WriteStartArray("actions");
						if (objectEvent.Actions != null) {
							foreach(UndertaleGameObject.EventAction action in objectEvent.Actions) {
								writer.WriteStartObject();
								if (action != null) {
									writer.WriteNumber("lib_id", action.LibID);
									writer.WriteNumber("id", action.ID);
									writer.WriteNumber("kind", action.Kind);
									writer.WriteBoolean("use_relative", action.UseRelative);
									writer.WriteBoolean("is_question", action.IsQuestion);
									writer.WriteBoolean("use_apply_to", action.UseApplyTo);
									writer.WriteNumber("exe_type", action.ExeType);
									
									WriteString(writer, "action_name", action.ActionName);
									
									if (action.CodeId != null) {
										WriteString(writer, "code_id", action.CodeId.Name);
									} else {
										writer.WriteNull("code_id");
									}
									
									writer.WriteNumber("argument_count", action.ArgumentCount);
									writer.WriteNumber("who", action.Who);
									writer.WriteBoolean("relative", action.Relative);
									writer.WriteBoolean("is_not", action.IsNot);
								}
								writer.WriteEndObject();
							}
						}
						writer.WriteEndArray();
					}
					writer.WriteEndObject();
				}
			}
			writer.WriteEndArray();
		}
	}
	writer.WriteEndArray();
	
	writer.WriteEndObject();
	
	writer.Flush();
	
	json = Encoding.UTF8.GetString(stream.ToArray());
	return json;
	return "";
}

// New assets have their full details placed in the info file and relevant assets added
Dictionary<string, AssetType> AssetsAddedDict = new Dictionary<string, AssetType>();
// Removed assets are added to the list of assets to be removed by the patch
Dictionary<string, AssetType> AssetsRemovedDict = new Dictionary<string, AssetType>();
// Assets which include changes to meta-parameters
Dictionary<string, AssetType> AssetsAlteredParametersDict = new Dictionary<string, AssetType>();
// Assets which include changes to core data
Dictionary<string, AssetType> AssetsAlteredDataDict = new Dictionary<string, AssetType>();

Dictionary<AssetType, Action<string, UndertaleData, UndertaleData>> AddParamsMethodDict = new Dictionary<AssetType, Action<string, UndertaleData, UndertaleData>> () {
	[AssetType.Sound]      = AddSoundParams,
	[AssetType.Path]       = AddPathParams,
	[AssetType.Code]       = DoNothing,
	[AssetType.Sprite]     = AddSpriteParams,
	[AssetType.Background] = AddBackgroundParams,
	[AssetType.Font]       = AddFontParams,
	[AssetType.Room]       = DoNothing,
	[AssetType.Shader]     = DoNothing,
	[AssetType.Extension]  = DoNothing,
	[AssetType.Timeline]   = DoNothing,
	[AssetType.GameObject] = DoNothing,
	[AssetType.Mask]       = DoNothing,
};

Dictionary<AssetType, Action<string, UndertaleData, UndertaleData>> AddDataMethodDict = new Dictionary<AssetType, Action<string, UndertaleData, UndertaleData>> () {
	[AssetType.Sound]      = AddSoundData,
	[AssetType.Path]       = AddPathData,
	[AssetType.Code]       = AddCodeData,
	[AssetType.Sprite]     = AddSpriteData,
	[AssetType.Background] = AddBackgroundData,
	[AssetType.Font]       = AddFontData,
	[AssetType.Room]       = AddRoomData,
	[AssetType.Shader]     = AddShaderData,
	[AssetType.Extension]  = DoNothing,
	[AssetType.Timeline]   = DoNothing,
	[AssetType.GameObject] = AddObjectData,
	[AssetType.Mask]       = AddMaskData,
};

Dictionary<string, IList<UndertaleEmbeddedAudio>> loadedVanillaAudioGroups = new Dictionary<string, IList<UndertaleEmbeddedAudio>>();;

Dictionary<string, IList<UndertaleEmbeddedAudio>> loadedModAudioGroups = new Dictionary<string, IList<UndertaleEmbeddedAudio>>();;

// // Keep track of cases where a modder deletes some but not all of an asset's data, or one part of a multi-part asset's data
// MarkPartialSpriteDataDeletion (assetName, numFramesLeft)
// MarkPartialMaskDataDeletion (assetName, numFramesLeft)
// MarkFontTextureDataDeletion (assetName)
// 
// // Keep track of cases where a modder deletes all of an asset's core data, but not the asset itself or its meta data
// MarkBackgroundTextureDeletion
// MarkPathPointsDeletion
// MarkSoundFileDeletion
// 
// // Keep track of total asset deletions
//

UndertaleSound GetDefaultSound() {
	UndertaleSound defaultSound = new UndertaleSound();
	defaultSound.Flags = UndertaleSound.AudioEntryFlags.IsEmbedded;
	defaultSound.Type = null;
	defaultSound.File = null;
	defaultSound.Effects = 0;
	defaultSound.Volume = 1;
	defaultSound.Preload = true;
	defaultSound.Pitch = 0;
	return defaultSound;
}

UndertalePath GetDefaultPath() {
	UndertalePath defaultPath = new UndertalePath();
	defaultPath.IsSmooth  = false;
	defaultPath.IsClosed  = false;
	defaultPath.Precision = 4;
	return defaultPath;
}

UndertaleSprite GetDefaultSprite() {
	UndertaleSprite defaultSprite = new UndertaleSprite();
	defaultSprite.Width                 = 0;
	defaultSprite.Height                = 0;
	defaultSprite.MarginLeft            = 0;
	defaultSprite.MarginRight           = 0;
	defaultSprite.MarginBottom          = 0;
	defaultSprite.MarginTop             = 0;
	defaultSprite.Transparent           = false;
	defaultSprite.Smooth                = false;
	defaultSprite.Preload               = false;
	defaultSprite.BBoxMode              = 0;
	defaultSprite.SepMasks              = UndertaleSprite.SepMaskType.AxisAlignedRect;
	defaultSprite.OriginX               = 0;
	defaultSprite.OriginY               = 0;
	defaultSprite.GMS2PlaybackSpeed     = 15.0f;
	defaultSprite.GMS2PlaybackSpeedType = AnimSpeedType.FramesPerSecond;
	return defaultSprite;
}

UndertaleBackground GetDefaultBackground() {
	UndertaleBackground defaultBackground = new UndertaleBackground();
	defaultBackground.Transparent       = false;
	defaultBackground.Smooth            = false;
	defaultBackground.Preload           = false;
	defaultBackground.GMS2TileWidth     = 32;
	defaultBackground.GMS2TileHeight    = 32;
	defaultBackground.GMS2OutputBorderX = 2;
	defaultBackground.GMS2OutputBorderY = 2;
	defaultBackground.GMS2TileColumns   = 32;
	defaultBackground.GMS2TileCount     = 1024;
	defaultBackground.GMS2FrameLength   = 66666;
	return defaultBackground;
}

UndertaleFont GetDefaultFont() {
	UndertaleFont defaultFont = new UndertaleFont();
	defaultFont.DisplayName  = new UndertaleString("Arial");
	defaultFont.EmSize       = 12;
	defaultFont.Bold         = false;
	defaultFont.Italic       = false;
	defaultFont.RangeStart   = 32;
	defaultFont.Charset      = 1;
	defaultFont.AntiAliasing = 1;
	defaultFont.RangeEnd     = 127;
	defaultFont.ScaleX       = 1;
	defaultFont.ScaleY       = 1;
	return defaultFont;
}

// Used as a placeholder if a particular asset doesn't use methods to change params or values

void DoNothing(string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	return;
}

// Methods to add params to <type>Info.txt files

void AddSoundParams(string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	List<string> paramNames = new List<string>();
	List<string> paramValues = new List<string>();
	UndertaleSound vanillaSound = VanillaData.Sounds.ByName(assetName);
	UndertaleSound modSound = ModData.Sounds.ByName(assetName);
	
	if (vanillaSound == null) {
		vanillaSound = GetDefaultSound();
	}
	
	if (vanillaSound.Flags != modSound.Flags) {
		paramNames.Add("flags");
		paramValues.Add(modSound.Flags.ToString());
	}
	if (vanillaSound.Type != modSound.Type) {
		if (vanillaSound.Type == null || modSound.Type == null) {
			paramNames.Add("type");
			paramValues.Add(modSound.Type == null ? null :  modSound.Type.Content);
		} else {
			if (!vanillaSound.Type.Content.Equals(modSound.Type.Content)) {
				paramNames.Add("type");
				paramValues.Add(modSound.Type.Content);
			}
		}
	}
	if (vanillaSound.File != modSound.File) {
		if (vanillaSound.File == null || modSound.File == null) {
			paramNames.Add("file");
			paramValues.Add(modSound.File == null ? null : modSound.File.Content);
		} else {
			if (!vanillaSound.File.Content.Equals(modSound.File.Content)) {
				paramNames.Add("file");
				paramValues.Add(modSound.File.Content);
			}
		}
	}
	
	if (vanillaSound.Effects != modSound.Effects) {
		paramNames.Add("effects");
		paramValues.Add(modSound.Effects.ToString());
	}
	if (vanillaSound.Volume != modSound.Volume) {
		paramNames.Add("volume");
		paramValues.Add(modSound.Volume.ToString());
	}
	if (vanillaSound.Preload != modSound.Preload) {
		paramNames.Add("preload");
		paramValues.Add(modSound.Preload.ToString());
	}
	if (vanillaSound.Pitch != modSound.Pitch) {
		paramNames.Add("pitch");
		paramValues.Add(modSound.Pitch.ToString());
	}
	if (vanillaSound.AudioGroup != modSound.AudioGroup) {
		if (vanillaSound.AudioGroup == null || modSound.AudioGroup == null) {
			paramNames.Add("audio_group");
			paramValues.Add(modSound.AudioGroup == null ? null :  modSound.AudioGroup.Name.Content);
		} else {
			if (!vanillaSound.AudioGroup.Name.Content.Equals(modSound.AudioGroup.Name.Content)) {
				paramNames.Add("audio_group");
				paramValues.Add(modSound.AudioGroup.Name.Content);
			}
		}
	}
	if (vanillaSound.AudioFile != modSound.AudioFile) {
		if (vanillaSound.AudioFile == null || modSound.AudioFile == null) {
			paramNames.Add("audio_file");
			paramValues.Add(modSound.AudioFile == null ? null :  modSound.AudioFile.Name.Content);
		} else {
			if (!vanillaSound.AudioFile.Name.Content.Equals(modSound.AudioFile.Name.Content)) {
				paramNames.Add("audio_file");
				paramValues.Add(modSound.AudioFile.Name.Content);
			}
		}
	}
	if (vanillaSound.AudioID != modSound.AudioID) {
		paramNames.Add("audio_id");
		paramValues.Add(modSound.AudioID.ToString());
	}
	if (vanillaSound.GroupID != modSound.GroupID) {
		paramNames.Add("group_id");
		paramValues.Add(modSound.GroupID.ToString());
	}
	
	if (paramNames.Count > 0) {
		WriteParamsToFile(soundInfoPath, assetName, paramNames, paramValues);
	}
}

void AddSpriteParams(string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	List<string> paramNames = new List<string>();
	List<string> paramValues = new List<string>();
	UndertaleSprite vanillaSprite = VanillaData.Sprites.ByName(assetName);
	UndertaleSprite modSprite = ModData.Sprites.ByName(assetName);
	
	if (vanillaSprite == null) {
		vanillaSprite = GetDefaultSprite();
	}
	// Figure out which params to add and what their values should be
	if (vanillaSprite.Width != modSprite.Width) {
		paramNames.Add("size_x");
		paramValues.Add(modSprite.Width.ToString());
	}
	if (vanillaSprite.Height != modSprite.Height) {
		paramNames.Add("size_y");
		paramValues.Add(modSprite.Height.ToString());
	}
	if (vanillaSprite.MarginLeft != modSprite.MarginLeft) {
		paramNames.Add("margin_left");
		paramValues.Add(modSprite.MarginLeft.ToString());
	}
	if (vanillaSprite.MarginRight != modSprite.MarginRight) {
		paramNames.Add("margin_right");
		paramValues.Add(modSprite.MarginRight.ToString());
	}
	if (vanillaSprite.MarginBottom != modSprite.MarginBottom) {
		paramNames.Add("margin_bottom");
		paramValues.Add(modSprite.MarginBottom.ToString());
	}
	if (vanillaSprite.MarginTop != modSprite.MarginTop) {
		paramNames.Add("margin_top");
		paramValues.Add(modSprite.MarginTop.ToString());
	}
	if (vanillaSprite.Transparent != modSprite.Transparent) {
		paramNames.Add("transparent");
		paramValues.Add(modSprite.Transparent.ToString());
	}
	if (vanillaSprite.Smooth != modSprite.Smooth) {
		paramNames.Add("smooth");
		paramValues.Add(modSprite.Smooth.ToString());
	}
	if (vanillaSprite.Preload != modSprite.Preload) {
		paramNames.Add("preload");
		paramValues.Add(modSprite.Preload.ToString());
	}
	if (vanillaSprite.BBoxMode != modSprite.BBoxMode) {
		paramNames.Add("bounding_box_mode");
		paramValues.Add(modSprite.BBoxMode.ToString());
	}
	if (vanillaSprite.SepMasks != modSprite.SepMasks) {
		paramNames.Add("sep_masks");
		paramValues.Add(modSprite.SepMasks.ToString());
	}
	if (vanillaSprite.OriginX != modSprite.OriginX) {
		paramNames.Add("origin_x");
		paramValues.Add(modSprite.OriginX.ToString());
	}
	if (vanillaSprite.OriginY != modSprite.OriginY) {
		paramNames.Add("origin_y");
		paramValues.Add(modSprite.OriginY.ToString());
	}
	if (vanillaSprite.GMS2PlaybackSpeed != modSprite.GMS2PlaybackSpeed) {
		paramNames.Add("playback_speed");
		paramValues.Add(modSprite.GMS2PlaybackSpeed.ToString());
	}
	if (vanillaSprite.GMS2PlaybackSpeedType != modSprite.GMS2PlaybackSpeedType) {
		paramNames.Add("playback_speed_type");
		paramValues.Add(modSprite.GMS2PlaybackSpeedType.ToString());
	}

	// Write our params if we need to.
	if (paramNames.Count > 0) {
		WriteParamsToFile(spriteInfoPath, assetName, paramNames, paramValues);
	}
}

void AddPathParams(string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	List<string> paramNames = new List<string>();
	List<string> paramValues = new List<string>();
	UndertalePath vanillaPath = VanillaData.Paths.ByName(assetName);
	UndertalePath modPath = ModData.Paths.ByName(assetName);
	
	if (vanillaPath == null) {
		vanillaPath = GetDefaultPath();
	}
	// Figure out which params to add and what their values should be
	if (vanillaPath.IsSmooth != modPath.IsSmooth) {
		paramNames.Add("smooth");
		paramValues.Add(modPath.IsSmooth.ToString());
	}
	if (vanillaPath.IsClosed != modPath.IsClosed) {
		paramNames.Add("closed");
		paramValues.Add(modPath.IsClosed.ToString());
	}
	if (vanillaPath.Precision != modPath.Precision) {
		paramNames.Add("precision");
		paramValues.Add(modPath.Precision.ToString());
	}

	// Write our params if we need to.
	if (paramNames.Count > 0) {
		WriteParamsToFile(pathInfoPath, assetName, paramNames, paramValues);
	}
}

void AddFontParams(string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	List<string> paramNames = new List<string>();
	List<string> paramValues = new List<string>();
	UndertaleFont vanillaFont = VanillaData.Fonts.ByName(assetName);
	UndertaleFont modFont = ModData.Fonts.ByName(assetName);
	
	if (vanillaFont == null) {
		vanillaFont = GetDefaultFont();
	}
	// Figure out which params to add and what their values should be
	if (vanillaFont.DisplayName != null && modFont.DisplayName != null) {
		if (!vanillaFont.DisplayName.Content.Equals(modFont.DisplayName.Content)) {
			paramNames.Add("display_name");
			paramValues.Add(modFont.DisplayName.ToString());
		}
	} else {
		if (vanillaFont.DisplayName == null) {
			paramNames.Add("display_name");
			paramValues.Add(modFont.DisplayName.ToString());
		}
	}
	if (vanillaFont.EmSize != modFont.EmSize) {
		paramNames.Add("font_size");
		paramValues.Add(modFont.EmSize.ToString());
	}
	if (vanillaFont.Bold != modFont.Bold) {
		paramNames.Add("bold");
		paramValues.Add(modFont.Bold.ToString());
	}
	if (vanillaFont.Italic != modFont.Italic) {
		paramNames.Add("italic");
		paramValues.Add(modFont.Italic.ToString());
	}
	if (vanillaFont.RangeStart != modFont.RangeStart) {
		paramNames.Add("range_start");
		paramValues.Add(modFont.RangeStart.ToString());
	}
	if (vanillaFont.RangeEnd != modFont.RangeEnd) {
		paramNames.Add("range_end");
		paramValues.Add(modFont.RangeEnd.ToString());
	}
	if (vanillaFont.Charset != modFont.Charset) {
		paramNames.Add("charset");
		paramValues.Add(modFont.Charset.ToString());
	}
	if (vanillaFont.AntiAliasing != modFont.AntiAliasing) {
		paramNames.Add("anti_aliasing");
		paramValues.Add(modFont.AntiAliasing.ToString());
	}
	if (vanillaFont.ScaleX != modFont.ScaleX) {
		paramNames.Add("scale_x");
		paramValues.Add(modFont.ScaleX.ToString());
	}
	if (vanillaFont.ScaleY != modFont.ScaleY) {
		paramNames.Add("scale_y");
		paramValues.Add(modFont.ScaleY.ToString());
	}

	// Write our params if we need to.
	if (paramNames.Count > 0) {
		WriteParamsToFile(fontInfoPath, assetName, paramNames, paramValues);
	}
}

void AddBackgroundParams(string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	List<string> paramNames = new List<string>();
	List<string> paramValues = new List<string>();
	UndertaleBackground vanillaBackground = VanillaData.Backgrounds.ByName(assetName);
	UndertaleBackground modBackground = ModData.Backgrounds.ByName(assetName);
	
	if (vanillaBackground == null) {
		vanillaBackground = GetDefaultBackground();
	}
	// Figure out which params to add and what their values should be
	if (vanillaBackground.Transparent != modBackground.Transparent) {
		paramNames.Add("transparent");
		paramValues.Add(modBackground.Transparent.ToString());
	}
	if (vanillaBackground.Smooth != modBackground.Smooth) {
		paramNames.Add("smooth");
		paramValues.Add(modBackground.Smooth.ToString());
	}
	if (vanillaBackground.Preload != modBackground.Preload) {
		paramNames.Add("preload");
		paramValues.Add(modBackground.Preload.ToString());
	}
	if (vanillaBackground.GMS2OutputBorderX != modBackground.GMS2OutputBorderX) {
		paramNames.Add("output_border_x");
		paramValues.Add(modBackground.GMS2OutputBorderX.ToString());
	}
	if (vanillaBackground.GMS2OutputBorderY != modBackground.GMS2OutputBorderY) {
		paramNames.Add("output_border_y");
		paramValues.Add(modBackground.GMS2OutputBorderY.ToString());
	}
	if (vanillaBackground.GMS2TileWidth != modBackground.GMS2TileWidth) {
		paramNames.Add("tile_width");
		paramValues.Add(modBackground.GMS2TileWidth.ToString());
	}
	if (vanillaBackground.GMS2TileHeight != modBackground.GMS2TileHeight) {
		paramNames.Add("tile_height");
		paramValues.Add(modBackground.GMS2TileHeight.ToString());
	}
	if (vanillaBackground.GMS2TileColumns != modBackground.GMS2TileColumns) {
		paramNames.Add("tile_columns");
		paramValues.Add(modBackground.GMS2TileColumns.ToString());
	}
	if (vanillaBackground.GMS2TileCount != modBackground.GMS2TileCount) {
		paramNames.Add("tile_count");
		paramValues.Add(modBackground.GMS2TileCount.ToString());
	}
	if (vanillaBackground.GMS2FrameLength != modBackground.GMS2FrameLength) {
		paramNames.Add("frame_time");
		paramValues.Add(modBackground.GMS2FrameLength.ToString());
	}
	// Write our params if we need to.
	if (paramNames.Count > 0) {
		WriteParamsToFile(bgInfoPath, assetName, paramNames, paramValues);
	}
}

// Methods to export core data files

void AddSoundData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	// Get sounds
	UndertaleSound modSound     = ModData.Sounds.ByName(assetName);
	UndertaleSound vanillaSound = VanillaData.Sounds.ByName(assetName);
	
	byte[] modSoundData = GetSoundData(modSound, ModData, ModDataPath, false);
	if (modSoundData == null) {
		// TODO: Handle this better
		//throw new Exception(String.Format("ERROR: {0}'s sound data was null, cannot export.", assetName));
		return;
	}
	
	string outPath = String.Format("{0}.snd", Path.Join(soundPath, assetName));
	
	File.WriteAllBytes(outPath, modSoundData);
}

byte[] GetSoundData (UndertaleSound sound, UndertaleData data, string dataPath, bool vanillaData) {
	if (sound == null) {
		return null;
	}
	if (data == null || dataPath == null) {
        throw new Exception(String.Format("ERROR: Sound {0} could not be linked to a data file and/or data path.", sound.Name.Content));
	}
	if (sound.GroupID > data.GetBuiltinSoundGroupID()) {
        IList<UndertaleEmbeddedAudio> audioGroup = GetAudioGroupData(sound, dataPath, vanillaData);
        if (audioGroup != null) {
            return audioGroup[sound.AudioID] == null ? null : audioGroup[sound.AudioID].Data;
		}
    } else {
		if (sound.AudioFile != null) {
			return sound.AudioFile.Data;
		}
	}
    return null;
}

bool DataNull (byte[] data) {
	foreach (byte dataByte in data) {
		if (dataByte != (byte)(0)) {
			return false;
		}
	}
	return true;
}

IList<UndertaleEmbeddedAudio> GetAudioGroupData(UndertaleSound sound, string winFolder, bool isVanillaData) {
	if (isVanillaData) {
		lock(loadedVanillaAudioGroups) {
			string audioGroupName = null;
			audioGroupName = sound.AudioGroup != null ? sound.AudioGroup.Name.Content : null;
			if (loadedVanillaAudioGroups.ContainsKey(audioGroupName)) {
				return loadedVanillaAudioGroups[audioGroupName];
			}
		
			string groupFilePath = Path.Combine(Path.GetDirectoryName(winFolder), "audiogroup" + sound.GroupID + ".dat");
			if (!File.Exists(groupFilePath)) {
				throw new Exception(String.Format("ERROR: Could not find audio group file for sound {0} - audio file expected at {1}", sound.Name.Content, groupFilePath));
			}
			try {
				UndertaleData data = null;
				using (var stream = new FileStream(groupFilePath, FileMode.Open, FileAccess.Read)) {
					data = UndertaleIO.Read(stream, warning => ScriptMessage(String.Format("WARNING: Warning delivered from OS while trying to load audio group file {0}:\n{1}", audioGroupName, warning)));
				}
				loadedVanillaAudioGroups[audioGroupName] = data.EmbeddedAudio;
				return data.EmbeddedAudio;
			} catch (Exception e) {
				throw new Exception(String.Format("ERROR: Could not load {0}:\n{1}", audioGroupName, e.Message));
				return null;
			}
		}
	} else {
		lock(loadedModAudioGroups) {
			string audioGroupName = null;
			audioGroupName = sound.AudioGroup != null ? sound.AudioGroup.Name.Content : null;
			if (loadedModAudioGroups.ContainsKey(audioGroupName)) {
				return loadedModAudioGroups[audioGroupName];
			}
			string groupFilePath = Path.Combine(Path.GetDirectoryName(winFolder), "audiogroup" + sound.GroupID + ".dat");
			if (!File.Exists(groupFilePath)) {
				throw new Exception(String.Format("ERROR: Could not find audio group file for sound {0} - audio file expected at {1}", sound.Name.Content, groupFilePath));
			}
			try {
				UndertaleData data = null;
				using (var stream = new FileStream(groupFilePath, FileMode.Open, FileAccess.Read)) {
					data = UndertaleIO.Read(stream, warning => ScriptMessage(String.Format("WARNING: Warning delivered from OS while trying to load audio group file {0}:\n{1}", audioGroupName, warning)));
				}
				loadedModAudioGroups[audioGroupName] = data.EmbeddedAudio;
				return data.EmbeddedAudio;
			} catch (Exception e) {
				throw new Exception(String.Format("ERROR: Could not load {0}:\n{1}", audioGroupName, e.Message));
				return null;
			}
		}
	}
}

void AddPathData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	StringBuilder sb = new StringBuilder();
	sb.Append(Path.Join(pathPath, assetName));
	sb.Append(".csv");
	StreamWriter sw = new StreamWriter(sb.ToString());
	
	foreach(UndertalePath.PathPoint point in ModData.Paths.ByName(assetName).Points) {
		sb.Clear();
		sb.Append(point.X.ToString());
		sb.Append(", ");
		sb.Append(point.Y.ToString());
		sb.Append(", ");
		sb.Append(point.Speed.ToString());
		sb.Append("\n");
		sw.Write(sb.ToString());
	}
	sw.Close();
}

void AddCodeData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	UndertaleCode modCode = ModData.Code.ByName(assetName);
	string outPath = Path.Combine(sourcePath, modCode.Name.Content + ".gml");
    try {
		GlobalDecompileContext context = new GlobalDecompileContext(ModData, false);
        File.WriteAllText(outPath, (modCode != null ? Decompiler.Decompile(modCode, context) : ""));
    }
    catch (Exception e)
	{
		CodeThatCouldNotBeDecompiled.Add(modCode.Name.Content);
        File.WriteAllText(outPath, "/*\nDECOMPILER FAILED!\n\n" + e.ToString() + "\n*/");
        ScriptMessage(String.Format("ERROR: Failed to decompile code entry {0}", assetName));
	}
}

void AddCodeThatGotErrors(List<string> splitStringsList, string pathToExtract) {
    if (ModDataPath == null)
    {
        throw new System.Exception("The mod's data path was not set.");
    }

    using (var stream = new FileStream(ModDataPath, FileMode.Open, FileAccess.Read))
    {
        ModData = UndertaleIO.Read(stream, warning => ScriptMessage("A warning occured while trying to load " + ModDataPath + ":\n" + warning));
    }


    if (ModData.IsYYC())
    {
        ScriptError("You cannot do a code dump of a YYC game! There is no code to dump!");
        return;
    }

    ThreadLocal<GlobalDecompileContext> DECOMPILE_CONTEXT = new ThreadLocal<GlobalDecompileContext>(() => new GlobalDecompileContext(ModData, false));


    if (pathToExtract == null)
        throw new ScriptException("The export folder was not set.");
    Directory.CreateDirectory(pathToExtract);

    List<String> codeToDump = new List<String>();
    List<String> gameObjectCandidates = new List<String>();

    for (var j = 0; j < splitStringsList.Count; j++)
    {
        foreach (UndertaleGameObject obj in ModData.GameObjects)
        {
            if (splitStringsList[j].ToLower() == obj.Name.Content.ToLower())
            {
                gameObjectCandidates.Add(obj.Name.Content);
            }
        }
        foreach (UndertaleScript scr in ModData.Scripts)
        {
            if (scr.Code == null)
                continue;
            if (splitStringsList[j].ToLower() == scr.Name.Content.ToLower())
            {
                codeToDump.Add(scr.Code.Name.Content);
            }
        }
        foreach (UndertaleGlobalInit globalInit in ModData.GlobalInitScripts)
        {
            if (globalInit.Code == null)
                continue;
            if (splitStringsList[j].ToLower() == globalInit.Code.Name.Content.ToLower())
            {
                codeToDump.Add(globalInit.Code.Name.Content);
            }
        }
        foreach (UndertaleCode code in ModData.Code)
        {
            if (splitStringsList[j].ToLower() == code.Name.Content.ToLower())
            {
                codeToDump.Add(code.Name.Content);
            }
        }
    }

    for (var j = 0; j < gameObjectCandidates.Count; j++)
    {
        try
        {
            UndertaleGameObject obj = ModData.GameObjects.ByName(gameObjectCandidates[j]);
            for (var i = 0; i < obj.Events.Count; i++)
            {
                foreach (UndertaleGameObject.Event evnt in obj.Events[i])
                {
                    foreach (UndertaleGameObject.EventAction action in evnt.Actions)
                    {
                        if (action.CodeId?.Name?.Content != null)
                            codeToDump.Add(action.CodeId?.Name?.Content);
                    }
                }
            }
        }
        catch
        {
            // Something went wrong, but probably because it's trying to check something non-existent
            // Just keep going
        }
    }


    for (var j = 0; j < codeToDump.Count; j++)
    {
        UndertaleCode code = ModData.Code.ByName(codeToDump[j]);
        string path = Path.Combine(pathToExtract, code.Name.Content + ".gml");
        if (code.ParentEntry == null)
        {
            try
            {
                File.WriteAllText(path, (code != null ? Decompiler.Decompile(code, DECOMPILE_CONTEXT.Value) : ""));
            }
            catch (Exception e)
            {
                if (!(Directory.Exists(Path.Combine(pathToExtract, "Failed"))))
                {
                    Directory.CreateDirectory(Path.Combine(pathToExtract, "Failed"));
                }
                path = Path.Combine(pathToExtract, "Failed", code.Name.Content + ".gml");
                File.WriteAllText(path, "/*\nDECOMPILER FAILED!\n\n" + e.ToString() + "\n*/");
            }
        }
        else
        {
            if (!(Directory.Exists(Path.Combine(pathToExtract, "Duplicates"))))
            {
                Directory.CreateDirectory(Path.Combine(pathToExtract, "Duplicates"));
            }
            try
            {
                path = Path.Combine(pathToExtract, "Duplicates", code.Name.Content + ".gml");
                File.WriteAllText(path, (code != null ? Decompiler.Decompile(code, DECOMPILE_CONTEXT.Value).Replace("@@This@@()", "self/*@@This@@()*/") : ""));
            }
            catch (Exception e)
            {
                if (!(Directory.Exists(Path.Combine(pathToExtract, "Duplicates", "Failed"))))
                {
                    Directory.CreateDirectory(Path.Combine(pathToExtract, "Duplicates", "Failed"));
                }
                path = Path.Combine(pathToExtract, "Duplicates", "Failed", code.Name.Content + ".gml");
                File.WriteAllText(path, "/*\nDECOMPILER FAILED!\n\n" + e.ToString() + "\n*/");
            }
        }


    }



}

void AddSpriteData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	UndertaleSprite vanillaSprite = VanillaData.Sprites.ByName(assetName);
	UndertaleSprite modSprite = ModData.Sprites.ByName(assetName);
	
	StringBuilder sb = new StringBuilder();
	
	if (vanillaSprite == null) {
		for (int i = 0; i < modSprite.Textures.Count; i++) {
			sb.Clear();
			sb.Append(Path.Join(spritePath, assetName));
			sb.Append("_");
			sb.Append(i.ToString());
			sb.Append(".png");
			worker.ExportAsPNG(ModData.Sprites.ByName(assetName).Textures[i].Texture, sb.ToString());
		}
	} else {
		int vanillaSpriteCount = vanillaSprite.Textures.Count;
		int modSpriteCount = modSprite.Textures.Count;
		if (vanillaSpriteCount > modSpriteCount) {
			for (int i = 0; i < modSpriteCount; i++) {
				if (!FrameEquals(vanillaSprite.Textures[i], modSprite.Textures[i])) {
					sb.Clear();
					sb.Append(Path.Join(spritePath, assetName));
					sb.Append("_");
					sb.Append(i.ToString());
					sb.Append(".png");
					worker.ExportAsPNG(ModData.Sprites.ByName(assetName).Textures[i].Texture, sb.ToString());
				}
			}
			// MarkPartialSpriteDeletion(assetName, modSpriteCount);
		} else {
			for (int i = 0; i < vanillaSpriteCount; i++) {
				if (!FrameEquals(vanillaSprite.Textures[i], modSprite.Textures[i])) {
					sb.Clear();
					sb.Append(Path.Join(spritePath, assetName));
					sb.Append("_");
					sb.Append(i.ToString());
					sb.Append(".png");
					worker.ExportAsPNG(ModData.Sprites.ByName(assetName).Textures[i].Texture, sb.ToString());
				}
			}
			for (int i = vanillaSpriteCount; i < modSpriteCount; i++) {
				sb.Clear();
				sb.Append(Path.Join(spritePath, assetName));
				sb.Append("_");
				sb.Append(i.ToString());
				sb.Append(".png");
				worker.ExportAsPNG(ModData.Sprites.ByName(assetName).Textures[i].Texture, sb.ToString());
			}
		}
	}
}

void AddBackgroundData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	StringBuilder sb = new StringBuilder();
	sb.Append(Path.Join(bgPath, assetName));
	sb.Append(".png");
	worker.ExportAsPNG(ModData.Backgrounds.ByName(assetName).Texture, sb.ToString());
}

void AddFontData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	UndertaleFont vanillaFont = VanillaData.Fonts.ByName(assetName);
	UndertaleFont modFont = ModData.Fonts.ByName(assetName);
	
	string outFileNoExt = Path.Join(fontPath, assetName);
	
	bool newGlyphs = false;
	bool newTexture = false;
	
	if (vanillaFont == null) {
		newGlyphs = true;
		newTexture = true;
	} else {
		newGlyphs = !FontGlyphEquals(vanillaFont, modFont);
		newTexture = !FontTextureEquals(vanillaFont, modFont);
	}
	
	if (newGlyphs) {
		StreamWriter sw = new StreamWriter(outFileNoExt + ".csv");
		StringBuilder glyphSb = new StringBuilder(48);
		if (modFont.Glyphs != null ) {
			foreach (UndertaleFont.Glyph glyph in modFont.Glyphs) {
				glyphSb.Clear();
				glyphSb.Append(glyph.Character.ToString());
				glyphSb.Append(", ");
				glyphSb.Append(glyph.SourceX.ToString());
				glyphSb.Append(", ");
				glyphSb.Append(glyph.SourceY.ToString());
				glyphSb.Append(", ");
				glyphSb.Append(glyph.SourceWidth.ToString());
				glyphSb.Append(", ");
				glyphSb.Append(glyph.SourceHeight.ToString());
				glyphSb.Append(", ");
				glyphSb.Append(glyph.Shift.ToString());
				glyphSb.Append(", ");
				glyphSb.Append(glyph.Offset.ToString());
				glyphSb.Append("\n");
				sw.Write(glyphSb.ToString());
			}
		} else {
			sw.Write("");
		}
		sw.Close();
	}
	
	if (newTexture) {
		if (modFont.Texture != null) {
			worker.ExportAsPNG(modFont.Texture, outFileNoExt + ".png");
		} else {
			// MarkFontTextureDataDeletion
		}
	}
}

void AddRoomData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	string pathOut = Path.Join(roomPath, assetName) + ".json";
	StreamWriter sw = new StreamWriter(pathOut);
	sw.Write(RoomToJson(ModData.Rooms.ByName(assetName)));
	sw.Close();
}

void AddShaderData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	
}

void AddObjectData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	string pathOut = Path.Join(objectPath, assetName) + ".json";
	StreamWriter sw = new StreamWriter(pathOut);
	sw.Write(GameObjectToJson(ModData.GameObjects.ByName(assetName)));
	sw.Close();
}

void AddMaskData (string assetName, UndertaleData VanillaData, UndertaleData ModData) {
	UndertaleSprite vanillaSprite = VanillaData.Sprites.ByName(assetName);
	UndertaleSprite modSprite = ModData.Sprites.ByName(assetName);
	
	StringBuilder sb = new StringBuilder();
	
	if (vanillaSprite == null) {
		for (int i = 0; i < modSprite.CollisionMasks.Count; i++) {
			sb.Clear();
			sb.Append(Path.Join(spritePath, assetName));
			sb.Append("_");
			sb.Append(i.ToString());
			sb.Append(".msk");
			File.WriteAllBytes(sb.ToString(), modSprite.CollisionMasks[i].Data);
		}
	} else {
		int vanillaSpriteCount = vanillaSprite.CollisionMasks.Count;
		int modSpriteCount = modSprite.CollisionMasks.Count;
		if (vanillaSpriteCount > modSpriteCount) {
			for (int i = 0; i < modSpriteCount; i++) {
				if (!MaskEquals(vanillaSprite.CollisionMasks[i], modSprite.CollisionMasks[i])) {
					sb.Clear();
					sb.Append(Path.Join(spritePath, assetName));
					sb.Append("_");
					sb.Append(i.ToString());
					sb.Append(".msk");
					File.WriteAllBytes(sb.ToString(), modSprite.CollisionMasks[i].Data);
				}
			}
			// MarkPartialSpriteDeletion(assetName, modSpriteCount);
		} else {
			for (int i = 0; i < vanillaSpriteCount; i++) {
				if (!MaskEquals(vanillaSprite.CollisionMasks[i], modSprite.CollisionMasks[i])) {
					sb.Clear();
					sb.Append(Path.Join(spritePath, assetName));
					sb.Append("_");
					sb.Append(i.ToString());
					sb.Append(".msk");
					File.WriteAllBytes(sb.ToString(), modSprite.CollisionMasks[i].Data);
				}
			}
			for (int i = vanillaSpriteCount; i < modSpriteCount; i++) {
				sb.Clear();
				sb.Append(Path.Join(spritePath, assetName));
				sb.Append("_");
				sb.Append(i.ToString());
				sb.Append(".msk");
				File.WriteAllBytes(sb.ToString(), modSprite.CollisionMasks[i].Data);
			}
		}
	}
}

void WriteParamsToFile(string infoPath, string assetName, List<string> paramNames, List<string> paramValues) {
	StringBuilder sb = new StringBuilder(512);
	StreamWriter sw = new StreamWriter(new FileStream(infoPath, FileMode.Append), Encoding.Default);
	
	if (paramNames.Count != paramValues.Count) {
		throw new Exception(String.Format("ERROR: paramNames and paramValues for parameters being written to {0} have unequal length!", infoPath));
	}
	
	int maxParamNameLength = 0;
	foreach (string paramName in paramNames) {
		if (paramName.Length > maxParamNameLength) {
			maxParamNameLength = paramName.Length;
		}
	}
	sb.Append(assetName);
	sb.Append(" {\n");
	for(int i = 0; i < paramNames.Count; i++) {
		sb.Append("\t");
		sb.Append(paramNames[i]);
		sb.Append(' ', maxParamNameLength - paramNames[i].Length);
		sb.Append(" : ");
		if (paramValues[i] == null) {
			sb.Append("null");
		} else {
			sb.Append(paramValues[i]);
		}
		sb.Append("\n");
	}
	sb.Append("}\n\n");
	sw.Write(sb.ToString());
	sw.Close();
}

void ExportPatchData(string path, UndertaleData VanillaData, UndertaleData ModData) {
	
	Directory.CreateDirectory(path);
	
	Directory.CreateDirectory(spritePath);
	Directory.CreateDirectory(fontPath);
	Directory.CreateDirectory(bgPath);
	Directory.CreateDirectory(maskPath);
	Directory.CreateDirectory(pathPath);
	Directory.CreateDirectory(roomPath);
	Directory.CreateDirectory(objectPath);
	Directory.CreateDirectory(soundPath);
	Directory.CreateDirectory(sourcePath);
	
	
	List<KeyValuePair<string, AssetType>> AssetsToChangeParameters = AssetsAddedDict.ToList().Concat(AssetsAlteredParametersDict.ToList()).ToList();
	List<KeyValuePair<string, AssetType>> AssetsToChangeData		= AssetsAddedDict.ToList().Concat(AssetsAlteredDataDict.ToList()).ToList();
	StringBuilder sb = new StringBuilder();
	foreach(KeyValuePair<string, AssetType> asset in AssetsToChangeParameters) {
		(AddParamsMethodDict[asset.Value])(asset.Key, VanillaData, ModData);
		sb.Append(asset.Key);
		sb.Append(" had params exported!\n");
	}
	// ScriptMessage(sb.ToString());
	sb.Clear();
	foreach(KeyValuePair<string, AssetType> asset in AssetsToChangeData) {
		(AddDataMethodDict[asset.Value])(asset.Key, VanillaData, ModData);
		sb.Append(asset.Key);
		sb.Append(" had data exported!\n");
	}
	// ScriptMessage(sb.ToString());
}

// Find and categorize every change we can

// Use this because I can't directly reassign the lists in the actual UndertaleData objects.

class TempDataContainer {
	public IList<UndertaleCode> Code;
	public IList<UndertaleSprite> Sprites;
	public IList<UndertaleSound> Sounds;
	public IList<UndertaleBackground> Backgrounds;
	public IList<UndertaleFont> Fonts;
	public IList<UndertalePath> Paths;
	public IList<UndertaleRoom> Rooms;
	public IList<UndertaleGameObject> GameObjects;
}



void CompareAllCode(List<UndertaleCode> importantVanillaCode, List<UndertaleCode> importantModCode, TempDataContainer tempVanillaData, TempDataContainer tempModData, UndertaleData VanillaData, UndertaleData ModData) {
    foreach (UndertaleCode code in importantModCode) {
		CompareCodeForAddition(code, tempVanillaData, tempModData, VanillaData, ModData);
    }
	
    foreach (UndertaleCode code in importantVanillaCode)  {
		CompareCodeForRemoval(code, tempVanillaData, tempModData);
    }
}

void CompareCodeForAddition(UndertaleCode modCode, TempDataContainer tempVanillaData, TempDataContainer tempModData, UndertaleData VanillaData, UndertaleData ModData) {
	UndertaleCode vanillaCode = tempVanillaData.Code.ByName(modCode.Name.Content);
	if (vanillaCode == null) {
		if (!AssetsAlteredDataDict.ContainsKey(modCode.Name.Content)) {
			AssetsAlteredDataDict.Add(modCode.Name.Content, AssetType.Code);
		}
	} else {
		
		if (!CodeEquals(vanillaCode, modCode, VanillaData, ModData)) {
			if (!AssetsAlteredDataDict.ContainsKey(modCode.Name.Content)) {
				AssetsAlteredDataDict.Add(modCode.Name.Content, AssetType.Code);
			}
		}
	}
}

void CompareCodeForRemoval(UndertaleCode vanillaCode, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleCode modCode = tempModData.Code.ByName(vanillaCode.Name.Content);
	if (modCode == null) {
		AssetsRemovedDict.Add(vanillaCode.Name.Content, AssetType.Code);
	}
}

async Task CompareAllSoundsAsync(TempDataContainer tempVanillaData, TempDataContainer tempModData) {
    await Task.Run(() => Parallel.ForEach(tempModData.Sounds,     sound => CompareSoundForAddition(sound, tempVanillaData, tempModData)));
    await Task.Run(() => Parallel.ForEach(tempVanillaData.Sounds, sound => CompareSoundForRemoval(sound, tempVanillaData, tempModData)));
}

void CompareSoundForAddition(UndertaleSound modSound, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleSound vanillaSound = tempVanillaData.Sounds.ByName(modSound.Name.Content);
	if (vanillaSound == null) {
		lock (AssetsAddedDict) {
			AssetsAddedDict.Add(modSound.Name.Content, AssetType.Sound);
		}
	} else {
		if (!SoundParamsEquals(vanillaSound, modSound)) {
			lock (AssetsAlteredParametersDict) {
				AssetsAlteredParametersDict.Add(modSound.Name.Content, AssetType.Sound);
			}
		}
		if (!SoundEquals(vanillaSound, modSound)) {
			lock (AssetsAlteredDataDict) {
				AssetsAlteredDataDict.Add(modSound.Name.Content, AssetType.Sound);
			}
		}
	}
}

void CompareSoundForRemoval(UndertaleSound vanillaSound, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleSound modSound = tempModData.Sounds.ByName(vanillaSound.Name.Content);
	if (modSound == null) {
		lock(AssetsRemovedDict) {
			AssetsRemovedDict.Add(vanillaSound.Name.Content, AssetType.Sound);
		}
	}
}

async Task CompareAllSpritesAsync(TempDataContainer tempVanillaData, TempDataContainer tempModData) {
    await Task.Run(() => Parallel.ForEach(tempModData.Sprites,        sprite => CompareSpriteForAddition(sprite, tempVanillaData, tempModData)));
    await Task.Run(() => Parallel.ForEach(tempVanillaData.Sprites,    sprite => CompareSpriteForRemoval(sprite, tempVanillaData, tempModData)));
}

void CompareSpriteForAddition(UndertaleSprite modSprite, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleSprite vanillaSprite = tempVanillaData.Sprites.ByName(modSprite.Name.Content);
	if (vanillaSprite == null) {
		lock(AssetsAddedDict) {
			AssetsAddedDict.Add(modSprite.Name.Content, AssetType.Sprite);
		}
	} else {
		if (!SpriteParamEquals(vanillaSprite, modSprite)) {
			lock(AssetsAlteredParametersDict) {
				AssetsAlteredParametersDict.Add(modSprite.Name.Content, AssetType.Sprite);
			}
		}
		if (!AllSpriteEquals(vanillaSprite, modSprite)) {
			lock(AssetsAlteredDataDict) {
				AssetsAlteredDataDict.Add(modSprite.Name.Content, AssetType.Sprite);
			}
			return;
		}
		if (!AllMaskEquals(vanillaSprite, modSprite)) {
			lock(AssetsAlteredDataDict) {
				AssetsAlteredDataDict.Add(modSprite.Name.Content, AssetType.Mask);
			}
			return;
		}
	}
}

void CompareSpriteForRemoval(UndertaleSprite vanillaSprite, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleSprite modSprite = tempModData.Sprites.ByName(vanillaSprite.Name.Content);
	if (modSprite == null) {
		lock (AssetsRemovedDict) {
			AssetsRemovedDict.Add(vanillaSprite.Name.Content, AssetType.Sprite);
		}
	}
}

async Task CompareAllBackgroundsAsync(TempDataContainer tempVanillaData, TempDataContainer tempModData) {
    await Task.Run(() => Parallel.ForEach(tempModData.Backgrounds,     bg => CompareBackgroundForAddition(bg, tempVanillaData, tempModData)));
    await Task.Run(() => Parallel.ForEach(tempVanillaData.Backgrounds, bg => CompareBackgroundForRemoval(bg, tempVanillaData, tempModData)));
}

void CompareBackgroundForAddition(UndertaleBackground modBackground, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleBackground vanillaBackground = tempVanillaData.Backgrounds.ByName(modBackground.Name.Content);
	if (vanillaBackground == null) {
		lock (AssetsAddedDict) {
			AssetsAddedDict.Add(modBackground.Name.Content, AssetType.Background);
		}
	} else {
		if (!BackgroundTextureEquals(vanillaBackground, modBackground)) {
			lock (AssetsAlteredDataDict) {
				AssetsAlteredDataDict.Add(modBackground.Name.Content, AssetType.Background);
			}
		}
		if (!BackgroundParamEquals(vanillaBackground, modBackground)) {
			lock (AssetsAlteredParametersDict) {
				AssetsAlteredParametersDict.Add(modBackground.Name.Content, AssetType.Background);
			}
		}
	}
}

void CompareBackgroundForRemoval(UndertaleBackground vanillaBackground, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleBackground modBackground = tempModData.Backgrounds.ByName(vanillaBackground.Name.Content);
	if (modBackground == null) {
		lock (AssetsRemovedDict) {
			AssetsRemovedDict.Add(vanillaBackground.Name.Content, AssetType.Background);
		}
	}
}

async Task CompareAllFontsAsync(TempDataContainer tempVanillaData, TempDataContainer tempModData) {
    await Task.Run(() => Parallel.ForEach(tempModData.Fonts,     font => CompareFontForAddition(font, tempVanillaData, tempModData)));
    await Task.Run(() => Parallel.ForEach(tempVanillaData.Fonts, font => CompareFontForRemoval(font, tempVanillaData, tempModData)));
}

void CompareFontForAddition(UndertaleFont modFont, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleFont vanillaFont = tempVanillaData.Fonts.ByName(modFont.Name.Content);
	if (vanillaFont == null) {
		lock (AssetsAddedDict) {
			AssetsAddedDict.Add(modFont.Name.Content, AssetType.Font);
		}
	} else {
		if (!FontParamEquals(vanillaFont, modFont)) {
			lock (AssetsAlteredParametersDict) {
				AssetsAlteredParametersDict.Add(modFont.Name.Content, AssetType.Font);
			}
		}
		if (!FontTextureEquals(vanillaFont, modFont)) {
			lock (AssetsAlteredDataDict) {
				AssetsAlteredDataDict.Add(modFont.Name.Content, AssetType.Font);
			}
		}
		if (!FontGlyphEquals(vanillaFont, modFont)) {
			lock (AssetsAlteredDataDict) {
				AssetsAlteredDataDict.Add(modFont.Name.Content, AssetType.Font);
			}
		}
	}	
}

void CompareFontForRemoval(UndertaleFont vanillaFont, TempDataContainer tempVanillaData, TempDataContainer tempModData) {
	UndertaleFont modFont = tempModData.Fonts.ByName(vanillaFont.Name.Content);
	if (modFont == null) {
		lock (AssetsRemovedDict) {
			AssetsRemovedDict.Add(vanillaFont.Name.Content, AssetType.Font);
		}
	}
}



void CompareGameFiles(UndertaleData VanillaData, UndertaleData ModData) {
	TempDataContainer tempVanillaData = new TempDataContainer();
	TempDataContainer tempModData = new TempDataContainer();
	// Replace any null values from data with empty lists.
	// This helps me avoid a lot of needless if/then/else clutter for null checking.
	// TODO: Consider making this more robust?
	// First time I've used ternaries.
	for (int i = 0; i < 2; i++) {
		UndertaleData data         = i == 0 ? VanillaData     : ModData;
		TempDataContainer tempData = i == 0 ? tempVanillaData : tempModData;
		tempData.Code        = data.Code        == null ? new List<UndertaleCode>()       : data.Code;
		tempData.Sprites     = data.Sprites     == null ? new List<UndertaleSprite>()     : data.Sprites;
		tempData.Sounds      = data.Sounds      == null ? new List<UndertaleSound>()      : data.Sounds;
		tempData.Backgrounds = data.Backgrounds == null ? new List<UndertaleBackground>() : data.Backgrounds;
		tempData.Fonts       = data.Fonts       == null ? new List<UndertaleFont>()       : data.Fonts;
		tempData.Paths       = data.Paths       == null ? new List<UndertalePath>()       : data.Paths;
		tempData.Rooms       = data.Rooms       == null ? new List<UndertaleRoom>()       : data.Rooms;
		tempData.GameObjects = data.GameObjects == null ? new List<UndertaleGameObject>() : data.GameObjects;
	}
	
	
	// Get only parent code entries,
	// Since these are the only ones that matter.
	List<UndertaleCode> importantVanillaCode = new List<UndertaleCode>();
	List<UndertaleCode> importantModCode = new List<UndertaleCode>();
	foreach (UndertaleCode code in tempVanillaData.Code) {
		if (code.ParentEntry != null) {
			continue;
		}
		importantVanillaCode.Add(code);
	}
	foreach (UndertaleCode code in tempModData.Code) {
		if (code.ParentEntry != null) {
			continue;
		}
		importantModCode.Add(code);
	}
	// Check all of the assets for diffs.
	// Code
    CompareAllCode(importantVanillaCode, importantModCode, tempVanillaData, tempModData, VanillaData, ModData);

	// Sound
	UpdateProgressBar("Comparing Game Files", "Comparing Sound... (Step 2/8)", 2, 8);
	Task.Run(async () =>
    {
        await CompareAllSoundsAsync(tempVanillaData, tempModData);
    }).Wait();
	
	// Sprites
	UpdateProgressBar("Comparing Game Files", "Comparing Sprites... (Step 3/8)", 3, 8);
	Task.Run(async () =>
    {
        await CompareAllSpritesAsync(tempVanillaData, tempModData);
    }).Wait();
	
	// Backgrounds
	UpdateProgressBar("Comparing Game Files", "Comparing Backgrounds... (Step 4/8)", 4, 8);
	Task.Run(async () =>
    {
        await CompareAllBackgroundsAsync(tempVanillaData, tempModData);
    }).Wait();
	
	// Fonts
	UpdateProgressBar("Comparing Game Files", "Comparing Fonts... (Step 5/8)", 5, 8);
	Task.Run(async () =>
    {
        await CompareAllFontsAsync(tempVanillaData, tempModData);
    }).Wait();
	
	// Paths
	UpdateProgressBar("Comparing Game Files", "Comparing Paths... (Step 6/8)", 6, 8);

	
	foreach (UndertalePath modPath in tempModData.Paths) {
		UndertalePath vanillaPath = tempVanillaData.Paths.ByName(modPath.Name.Content);
		if (vanillaPath == null) {
			AssetsAddedDict.Add(modPath.Name.Content, AssetType.Path);
		} else {
			if (!AllPathPointEquals(vanillaPath, modPath)) {
				AssetsAlteredDataDict.Add(modPath.Name.Content, AssetType.Path);
			}
			if (!PathParamEquals(vanillaPath, modPath)) {
				AssetsAlteredParametersDict.Add(modPath.Name.Content, AssetType.Path);
			}
		}
	}
	foreach (UndertalePath vanillaPath in tempVanillaData.Paths) {
		UndertalePath modPath = tempModData.Paths.ByName(vanillaPath.Name.Content);
		if (modPath == null) {
			AssetsRemovedDict.Add(vanillaPath.Name.Content, AssetType.Path);
		}
	}
	// Rooms
	UpdateProgressBar("Comparing Game Files", "Comparing Rooms... (Step 7/8)", 7, 8);
	foreach (UndertaleRoom modRoom in tempModData.Rooms) {
		UndertaleRoom vanillaRoom = tempVanillaData.Rooms.ByName(modRoom.Name.Content);
		if (vanillaRoom == null) {
			AssetsAddedDict.Add(modRoom.Name.Content, AssetType.Room);
		} else {
			if (!RoomEquals(vanillaRoom, modRoom)) {
				AssetsAlteredDataDict.Add(modRoom.Name.Content, AssetType.Room);
			}
		}
	}
	foreach (UndertaleRoom vanillaRoom in tempVanillaData.Rooms) {
		UndertaleRoom modRoom = tempModData.Rooms.ByName(vanillaRoom.Name.Content);
		if (modRoom == null) {
			AssetsRemovedDict.Add(vanillaRoom.Name.Content, AssetType.Room);
		}
	}
	// Game Objects TODO
	UpdateProgressBar("Comparing Game Files", "Comparing GameObjects... (Step 8/8)", 8, 8);
	foreach (UndertaleGameObject modObj in tempModData.GameObjects) {
		UndertaleGameObject vanillaObj = tempVanillaData.GameObjects.ByName(modObj.Name.Content);
		if (vanillaObj == null) {
			AssetsAddedDict.Add(modObj.Name.Content, AssetType.GameObject);
		} else {
			if (!GameObjectEquals(vanillaObj, modObj)) {
				AssetsAlteredDataDict.Add(modObj.Name.Content, AssetType.GameObject);
			}
		}
	}
	foreach (UndertaleGameObject vanillaObj in tempVanillaData.GameObjects) {
		UndertaleGameObject modObj = tempModData.GameObjects.ByName(vanillaObj.Name.Content);
		if (modObj == null) {
			AssetsRemovedDict.Add(vanillaObj.Name.Content, AssetType.GameObject);
		}
	}
}



// Buncha methods for doing equality tests on various types of data in UMT,
// And collections/subsets of said data.

bool CodeEquals(UndertaleCode codeA, UndertaleCode codeB, UndertaleData VanillaData, UndertaleData ModData) {
	try {
		GlobalDecompileContext contextA = new GlobalDecompileContext(VanillaData, false);
		GlobalDecompileContext contextB = new GlobalDecompileContext(ModData    , false);
		
		string decompA = (codeA != null ? Decompiler.Decompile(codeA, contextA) : "");
		string decompB = (codeB != null ? Decompiler.Decompile(codeB, contextB) : "");

		return decompA.Equals(decompB);
	} catch (Exception e) {
		// ScriptMessage(String.Format("ERROR: Failed to decompile code entry {0}", codeA.Name.Content));
		return true; // if we can't decomp, we just assume scripts are the same.
	}
}

bool MagickImageEquals(IMagickImage<byte> imgA, IMagickImage<byte> imgB) {
	// Simplistic image comparison method, reliant on comparing pixel streams.
	if (imgA != imgB) {
		if (imgA != null && imgB != null) {
			IPixelCollection<byte> pixelsA = imgA.GetPixels();
			IPixelCollection<byte> pixelsB = imgB.GetPixels();
			if (imgA.Width == imgB.Width && imgA.Height == imgB.Height) {
				for (int i = 0; i < imgA.Width; i++) {
					for (int j = 0; j < imgA.Height; j++) {
						if (!pixelsA.GetPixel(i, j).Equals(pixelsB.GetPixel(i, j))) {
							return false;
						}
					}
				}
				return true;
			} else {
				return false;
			}
		} else {
			return false;
		}
	} else {
		return true;
	}
}

bool SpriteParamEquals (UndertaleSprite spriteA, UndertaleSprite spriteB) {
	return spriteA.Width == spriteB.Width
	    &&  spriteA.Height                == spriteB.Height
	    &&  spriteA.MarginLeft            == spriteB.MarginLeft
	    &&  spriteA.MarginRight           == spriteB.MarginRight
	    &&  spriteA.MarginBottom          == spriteB.MarginBottom
	    &&  spriteA.MarginTop             == spriteB.MarginTop
	    &&  spriteA.Transparent           == spriteB.Transparent
	    &&  spriteA.Smooth                == spriteB.Smooth
	    &&  spriteA.Preload               == spriteB.Preload
	    &&  spriteA.BBoxMode              == spriteB.BBoxMode
	    &&  spriteA.SepMasks              == spriteB.SepMasks
	    &&  spriteA.OriginX               == spriteB.OriginX
	    &&  spriteA.OriginY               == spriteB.OriginY
	    &&  spriteA.GMS2PlaybackSpeed     == spriteB.GMS2PlaybackSpeed
	    &&  spriteA.GMS2PlaybackSpeedType == spriteB.GMS2PlaybackSpeedType;
}

bool AllSpriteEquals (UndertaleSprite spriteA, UndertaleSprite spriteB) {
	if (spriteA.Textures.Count != spriteB.Textures.Count) {
		return false;
	}
	for (int i = 0; i < spriteA.Textures.Count; i++) {
		if (!FrameEquals(spriteA.Textures[i], spriteB.Textures[i])) {
			return false;
		}
	}
	return true;
}

bool AllMaskEquals (UndertaleSprite spriteA, UndertaleSprite spriteB) {
	if (spriteA.CollisionMasks.Count != spriteB.CollisionMasks.Count) {
		return false;
	}
	for (int i = 0; i < spriteA.CollisionMasks.Count; i++) {		
		if (!MaskEquals(spriteA.CollisionMasks[i], spriteB.CollisionMasks[i])) {
			return false;
		}
	}
	return true;
}

bool FrameEquals (UndertaleSprite.TextureEntry texEntryA, UndertaleSprite.TextureEntry texEntryB) {
	if (texEntryA != texEntryB) {
		if (texEntryA == null || texEntryB == null) {
			return false;
		}
		if (texEntryA.Texture != texEntryB.Texture) {
			if (texEntryA.Texture == null || texEntryB.Texture == null) {
				return false;
			}
			IMagickImage<byte> imgA = worker.GetTextureFor(texEntryA.Texture, "<CmpTexture>");
			IMagickImage<byte> imgB = worker.GetTextureFor(texEntryB.Texture, "<CmpTexture>");
			return MagickImageEquals(imgA, imgB);
		}
	}
	return true;
}

bool MaskEquals (UndertaleSprite.MaskEntry maskA, UndertaleSprite.MaskEntry maskB) {
	string maskA64 = Convert.ToBase64String(maskA.Data);
	string maskB64 = Convert.ToBase64String(maskB.Data);
	return string.Equals(maskA64, maskB64);
}

bool BackgroundTextureEquals (UndertaleBackground backgroundA, UndertaleBackground backgroundB) {
	if (backgroundA.Texture != backgroundB.Texture) {
		if (backgroundA.Texture == null || backgroundB.Texture == null) {
			return false;
		}
		IMagickImage<byte> imgA = worker.GetTextureFor(backgroundA.Texture, "<CmpTexture>");
		IMagickImage<byte> imgB = worker.GetTextureFor(backgroundB.Texture, "<CmpTexture>");
		return MagickImageEquals(imgA, imgB);
	}
	return true;
}

bool BackgroundParamEquals (UndertaleBackground backgroundA, UndertaleBackground backgroundB) {
	return backgroundA.GMS2OutputBorderX == backgroundB.GMS2OutputBorderX
		&&  backgroundA.GMS2OutputBorderY == backgroundB.GMS2OutputBorderY
		&&  backgroundA.Transparent       == backgroundB.Transparent
		&&  backgroundA.Smooth            == backgroundB.Smooth
		&&  backgroundA.Preload           == backgroundB.Preload
		&&  backgroundA.GMS2TileWidth     == backgroundB.GMS2TileWidth
		&&  backgroundA.GMS2TileHeight    == backgroundB.GMS2TileHeight
		&&  backgroundA.GMS2TileColumns   == backgroundB.GMS2TileColumns
		&&  backgroundA.GMS2TileCount     == backgroundB.GMS2TileCount
		&&  backgroundA.GMS2FrameLength   == backgroundB.GMS2FrameLength;
}

bool FontParamEquals (UndertaleFont fontA, UndertaleFont fontB) {
	return fontA.EmSize == fontB.EmSize
		&& fontA.Bold == fontB.Bold
		&& fontA.Italic == fontB.Italic
		&& fontA.Charset == fontB.Charset
		&& fontA.AntiAliasing == fontB.AntiAliasing
		&& fontA.RangeStart == fontB.RangeStart
		&& fontA.RangeEnd == fontB.RangeEnd
		&& fontA.ScaleX == fontB.ScaleX
		&& fontA.ScaleY == fontB.ScaleY
		&& fontA.DisplayName.Content.Equals(fontB.DisplayName.Content);
}

bool FontTextureEquals (UndertaleFont fontA, UndertaleFont fontB) {
	if (fontA.Texture != fontB.Texture) {
		if (fontA.Texture == null || fontB.Texture == null) {
			return false;
		}
		IMagickImage<byte> imgA = worker.GetTextureFor(fontA.Texture, "<CmpTexture>");
		IMagickImage<byte> imgB = worker.GetTextureFor(fontB.Texture, "<CmpTexture>");
		return MagickImageEquals(imgA, imgB);
	}
	return true;
}

bool FontGlyphEquals (UndertaleFont fontA, UndertaleFont fontB) {
	if (fontA.Glyphs.Count != fontB.Glyphs.Count) {
		return false;
	}
	for (int i = 0; i < fontA.Glyphs.Count; i++) {
		if (!GlyphEquals(fontA.Glyphs[i], fontB.Glyphs[i])) {
			return false;
		}
	}
	return true;
}

bool GlyphEquals(UndertaleFont.Glyph glyphA, UndertaleFont.Glyph glyphB) {
	return glyphA.Character == glyphB.Character
		&& glyphA.SourceX == glyphB.SourceX
		&& glyphA.SourceY == glyphB.SourceY
		&& glyphA.SourceWidth == glyphB.SourceWidth
		&& glyphA.SourceHeight == glyphB.SourceHeight
		&& glyphA.Shift == glyphB.Shift
		&& glyphA.Offset == glyphB.Offset;
}

bool AllPathPointEquals (UndertalePath pathA, UndertalePath pathB) {
	if (pathA.Points.Count != pathB.Points.Count) {
		return false;
	}
	for (int i = 0; i < pathA.Points.Count; i++) {
		if (!PathPointEquals(pathA.Points[i], pathB.Points[i])) {
			return false;
		}
	}
	return true;
}

bool PathParamEquals (UndertalePath pathA, UndertalePath pathB) {
	return pathA.IsSmooth == pathB.IsSmooth
		&& pathA.IsClosed == pathB.IsClosed
		&& pathA.Precision == pathB.Precision;
}

bool PathPointEquals (UndertalePath.PathPoint pointA, UndertalePath.PathPoint pointB) {
	return pointA.X == pointB.X
		&& pointA.Y == pointB.Y
		&& pointA.Speed == pointB.Speed;
}

bool SoundParamsEquals (UndertaleSound soundA, UndertaleSound soundB) {
	if (soundA.AudioGroup != soundB.AudioGroup) {
		if (soundA.AudioGroup == null || soundB.AudioGroup == null) {
			return false;
		}
		if (!soundA.AudioGroup.Name.Content.Equals(soundB.AudioGroup.Name.Content)) {
			return false;
		}
	}
	if (soundA.Type != soundB.Type) {
		if (soundA.Type == null || soundB.Type == null) {
			return false;
		}
		if (!soundA.Type.Content.Equals(soundB.Type.Content)) {
			return false;
		}
	}
	if (soundA.File != soundB.File) {
		if (soundA.File == null || soundB.File == null) {
			return false;
		}
		if (!soundA.File.Content.Equals(soundB.File.Content)) {
			return false;
		}
	}
	if (soundA.AudioFile != soundB.AudioFile) {
		if (soundA.AudioFile == null || soundB.AudioFile == null) {
			return false;
		}
		if (!soundA.AudioFile.Name.Content.Equals(soundB.AudioFile.Name.Content)) {
			return false;
		}
	}
	return soundA.Flags == soundB.Flags
		&& soundA.Effects == soundB.Effects
		&& soundA.Volume == soundB.Volume
		&& soundA.Preload == soundB.Preload
		&& soundA.Pitch == soundB.Pitch
		&& soundA.AudioID == soundB.AudioID
		&& soundA.GroupID == soundB.GroupID;
}

bool SoundEquals (UndertaleSound soundA, UndertaleSound soundB) {
	if (soundA != soundB) {
		if (soundA == null || soundB == null) {
			return false;
		} else {
			byte[] audioFileA = GetSoundData(soundA, VanillaData, VanillaDataPath, true);
			byte[] audioFileB = GetSoundData(soundB, ModData, ModDataPath, false);
			if (audioFileA == null && audioFileB == null) {
				return true;
			}
			if (audioFileA == null || audioFileB == null) {
				return false;
			}
			string audioFileAString = Convert.ToBase64String(audioFileA);
			string audioFileBString = Convert.ToBase64String(audioFileB);
			bool sameSoundData = audioFileAString.Equals(audioFileBString);
			return sameSoundData;
		}
	}
	return true;
}

// Evaluate equality specifically for rooms

bool RoomEquals (UndertaleRoom roomA, UndertaleRoom roomB) {
	if (roomA != roomB) {
		if (roomA == null || roomB == null) {
			return false;
		}
		return RoomToJson(roomA).Equals(RoomToJson(roomB));
	}
	return true;
}

bool GameObjectEquals(UndertaleGameObject objectA, UndertaleGameObject objectB) {
	if (objectA != objectB) {
		if (objectA == null || objectB == null) {
			return false;
		}
		return GameObjectToJson(objectA).Equals(GameObjectToJson(objectB));
	}
	return true;
}


///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY ////  MAIN CODE BODY //
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


ScriptMessage("Select the vanilla game's data.win");

UndertaleData VanillaData;
string VanillaDataPath = PromptLoadFile(null, null);
if (VanillaDataPath == null) {
	throw new System.Exception("The vanilla game's data path was not set.");
}

using (var stream = new FileStream(VanillaDataPath, FileMode.Open, FileAccess.Read)) {
	VanillaData = UndertaleIO.Read(stream, warning => ScriptMessage("A warning occured while trying to load " + VanillaDataPath + ":\n" + warning));
}
// If true, will throw an error if a size mismatch occurs.

ScriptMessage("Select the modded game's data.win");
UndertaleData ModData;
string ModDataPath = PromptLoadFile(null, null);
if (ModDataPath == null) {
	throw new System.Exception("The mod's data path was not set.");
}

using (var stream = new FileStream(ModDataPath, FileMode.Open, FileAccess.Read)) {
	ModData = UndertaleIO.Read(stream, warning => ScriptMessage("A warning occured while trying to load " + ModDataPath + ":\n" + warning));
}

ScriptMessage("Select the patch output directory");
string PatchOutputPath = PromptChooseDirectory("Export to where");

if (PatchOutputPath == null) {
	throw new System.Exception("The patch's output path was not set.");
}

string spritePath = Path.Join(PatchOutputPath, "spr");
string fontPath   = Path.Join(PatchOutputPath, "fnt");
string bgPath     = Path.Join(PatchOutputPath, "bg");
string maskPath   = Path.Join(PatchOutputPath, "mask");
string pathPath   = Path.Join(PatchOutputPath, "path");
string roomPath   = Path.Join(PatchOutputPath, "room");
string objectPath = Path.Join(PatchOutputPath, "obj");
string soundPath  = Path.Join(PatchOutputPath, "snd");
string sourcePath = Path.Join(PatchOutputPath, "src");

string spriteInfoPath = Path.Join(spritePath, "spriteInfo.txt");
string bgInfoPath     = Path.Join(bgPath, "bgInfo.txt");
string fontInfoPath   = Path.Join(fontPath, "fntInfo.txt");
string pathInfoPath   = Path.Join(pathPath, "pathInfo.txt");	
string soundInfoPath  = Path.Join(soundPath, "soundInfo.txt");

string[] infoFiles = {spriteInfoPath, bgInfoPath, fontInfoPath, pathInfoPath, soundInfoPath};
foreach (string infoFile in infoFiles) {
	if (File.Exists(infoFile)) {
		File.Delete(infoFile);
	}
}

bool keepGoing = ScriptQuestion("WARNING: The patch creation process cannot be cancelled, and can last anywhere from 5 minutes to over an hour. Are you sure you want to proceed?\n\nNOTE: Due to high CPU usage, the progress bar is likely to desync from the script's actual progress, and some steps of the script take much longer than others.");
if (!keepGoing) {
	ScriptMessage("Patch creation cancelled.");
	return;
}

// Basic Checks
if (ModData.GeneralInfo.Major != VanillaData.GeneralInfo.Major) {
	keepGoing = ScriptQuestion(String.Format("WARNING: The source and mod files have differing major versions.\nMod data is version {0} while Vanilla data is version {1}\n\nDo you wish to continue?", ModData.GeneralInfo.Major, VanillaData.GeneralInfo.Major));
	if (keepGoing) {
		ScriptQuestion("WARNING: There may be serious compatibility issues converting between major versions. Be sure to test your mod thoroughly!");
	} else {
		ScriptMessage("Patch creation cancelled.");
		return;
	}
}

// Audio Checks
if ((ModData.AudioGroups.ByName("audiogroup_default") == null) && ModData.GeneralInfo.Major >= 2) {
    throw new Exception("Vanilla data file has no \"audiogroup_default\" but it is GMS2 or greater. AudioGroups count: " + ModData.AudioGroups.Count.ToString());
}
if ((VanillaData.AudioGroups.ByName("audiogroup_default") == null) && VanillaData.GeneralInfo.Major >= 2) {
    throw new Exception("Mod data file has no \"audiogroup_default\" but it is GMS2 or greater. AudioGroups count: " + ModData.AudioGroups.Count.ToString());
}

UpdateProgressBar("Comparing Game Files", "Comparing Code... (Step 1/8)", 1, 8);
CompareGameFiles(VanillaData, ModData);
UpdateProgressBar("Creating Patch", "Creating Patch Files from Differences...", 1, 1);
ExportPatchData(PatchOutputPath, VanillaData, ModData);

AddCodeThatGotErrors(CodeThatCouldNotBeDecompiled, sourcePath);
bool forceMatchingSpriteSize = ScriptQuestion(".cfg Creation\n\nWould you like to enable force_matching_sprite_size?\nThis can be helpful for debugging, as it will throw an error if there is a mismatch between sprite sizes while applying a patch.");



StreamWriter sw = new StreamWriter(Path.Join(PatchOutputPath, "patch.cfg"));
sw.Write("// This part of the .cfg is generated automatically by the patch exporter.\n");
sw.Write("// Changing it could have unforseen consequences - please avoid doing so.\n");
sw.Write("patch_version 1.0\n\n");
sw.Write("// Set to true to force modified sprites to match the original size.\n");
sw.Write("// This affects behavior only at time of patch application, not initial patch creation.\n");
sw.Write("// If false, will stretch or squish the frames to fit sprite size\n");
sw.Write("// If true, will throw an error if a size mismatch occurs.\n");
sw.Write("// This applies to font and background images as well.\n\n");
sw.Write("force_matching_sprite_size ");
sw.Write(forceMatchingSpriteSize.ToString());
sw.Write("\n");
sw.Close();



UpdateProgressBar("Creating Config File", "Creating Patch Files from Differences...", 1, 1);
ScriptMessage("Patch Creation Complete");
HideProgressBar();