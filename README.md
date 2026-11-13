# Hoardkeeper

A client-side BepInEx mod for Valheim: every chest around you in one window, with search, chest tags and names, sort, stack, restock, slot locks and favorites.

Mod para Valheim (BepInEx, client-side): todos os baús ao seu redor numa janela só, com busca, tags e nomes de baú, organizar, empilhar, reabastecer, espaços travados e favoritos.

For players, see [package/README.md](package/README.md). That file is also the Thunderstore page.
Para jogadores, veja [package/README.md](package/README.md).

## Building

`sh build.sh` compiles `src/*.cs` against the game's assemblies and installs the dll into `BepInEx/plugins/Hoardkeeper/` (`sh build.sh nocopy` only compiles). Then `powershell -File pack.ps1` builds `dist/Hoardkeeper-<version>.zip` from `package/`.

## License

MIT
