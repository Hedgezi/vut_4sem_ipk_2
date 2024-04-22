.PHONY: all clean

TARGET = ./ipk24chat-server
TARGET_DIR = ./ipk24chat
PROJECT_NAME = vut_ipk2
PROJECT_FILE = ./$(PROJECT_NAME).csproj

all: $(TARGET)

$(TARGET):
	dotnet publish $(PROJECT_FILE) -c Release -r linux-x64 -p:PublishSingleFile=true -p:DebugType=none --self-contained false -o $(TARGET_DIR) --nologo -v q
	mv $(TARGET_DIR)/$(PROJECT_NAME) $(TARGET)
	rm -rf $(TARGET_DIR)

clean:
	dotnet clean $(PROJECT_FILE)
	rm -rf $(TARGET)