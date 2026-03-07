{
  description = "Voice chat client";
  inputs = {
    nixpkgs.url = "nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };
  outputs = {
    nixpkgs,
    flake-utils,
    ...
  }:
    flake-utils.lib.eachDefaultSystem (
      system: let
        pkgs = import nixpkgs {inherit system; config.allowUnfree = true;};
        dotnet-sdk = pkgs.dotnet-sdk_10;
        podman = pkgs.podman;
        podman-tui = pkgs.podman-tui;
        compose = pkgs.docker-compose;
        postgresql = pkgs.postgresql;
        
        commonShellHook = ''
          export DOTNET_ENVIRONMENT="Development"
        '';
        
        macosShellHook = commonShellHook + ''
          # macOS specific setup
          mkdir -p ~/Library/Application\ Support/podman
          if ! podman machine list | grep -q "Currently running"; then
            podman machine init || true
            podman machine start || true
          fi
        '';
        
        linuxShellHook = commonShellHook + ''
          # Linux setup
          mkdir -p $XDG_RUNTIME_DIR/podman
          touch $XDG_RUNTIME_DIR/podman/podman.sock
          podman system service --time=0 unix://$XDG_RUNTIME_DIR/podman/podman.sock &
        '';
        
        shellHook = if builtins.match ".*-darwin" system != null then macosShellHook else linuxShellHook;
      in {
        devShells = {
          default = pkgs.mkShell {
            buildInputs = [dotnet-sdk podman podman-tui compose postgresql];
            inherit shellHook;
          };
        };
      }
    );
}
