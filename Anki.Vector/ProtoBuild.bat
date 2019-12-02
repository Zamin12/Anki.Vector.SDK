protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/messages.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/cube.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/response_status.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/extensions.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/alexa.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/behavior.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/nav_map.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/settings.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/shared.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe
protoc.exe -I./proto --csharp_out ./ExternalInterface --grpc_out ./ExternalInterface ./proto/anki_vector/messaging/external_interface.proto --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe