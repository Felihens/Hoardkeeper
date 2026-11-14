# Hoardkeeper

**[English](#english) · [Português](#português)**

---

## English

Every chest around you in one window. Sort, stack, restock, tag and name your chests, and find any item in your base without opening chest after chest.

### Features

**Storage window** (Shift + F, or the *Storage* button next to your inventory)
- Shows every chest within 50 m in a grid, two or more side by side, with the game's own chest slots. Drag, split and Ctrl + click work as in a normal chest.
- **Search box:** items that match light up, and the rest fade. Chests holding the item move to the top.
- **Right-click an item** to light up that item in every chest and see how many you have in total. Those chests move to the top too.
- **Left-side commands:**
  - *Sort all*
  - *Tidy up*: gathers each item into the chest that holds most of it.
  - *Stack*
  - *Fill tags*
  - *Filter*: shows only the chests with one tag.
  - *Bag on/off*: whether Gather also takes from your bag.
  - *Close*
- **Buttons on each chest:**
  - *Gather*: brings here what other chests hold of this chest's items.
  - *Sort*
  - *Take all*
  - *Tags*
  - *Name*
  - *Skip*: hides the chest from the window and from Stack and Restock, for you only.
- Ctrl + click an item in your bag sends it to a chest tagged for it, or else to a chest that already holds it.
- Under your bag, a short list shows your last Ctrl + click moves and which chest each item went to.

**Chest tags**
- Give a chest one or more kinds of item, from the Storage window or from the chest's own window. The 20 kinds are Weapons, Ammo, Armor, Tools, Food, Meads, Meat, Fish, Crops, Seeds, Ores, Metals, Wood, Stone, Hides, Creature (creature parts), Valuables, Trophies, Materials and Misc.
- **Fill tags** sends every item to the chest tagged for its kind, from your bag and from the other chests.
- **Fill tags in a chest's own window** fills just that chest.

**Chest names**
- Name a chest from the Storage window, or with the *Name* button in the chest's own window.
- The name shows when you look at the chest and on its window.

**Bag and chests**
- **T** sorts the open chest, or your bag. **Shift + T** (or Shift + click *Sort*) changes the order: type, name A–Z or Z–A, quality, weight, amount or value.
- **Stack** (Shift + Q) puts your items into nearby chests that already hold them.
- **Restock** (Shift + R) tops up your hotbar, ammo and locked slots from nearby chests.
- **Store all** button, and a **trash can**: drop an item on it and confirm to destroy it.
- Optional auto sort when you open the inventory or a chest, and after you pick something up.

**Your items stay put**
- **Alt + click** locks a bag slot.
- **Alt + right-click** marks a kind of item as a favorite, shown with a gold diamond.
- Nothing moves your hotbar, equipped, locked or favorite items, or items in extra equipment slots from other mods.

**Plays fair**
- Chests in use by another player, other players' private chests and chests inside wards you can't open are left alone.
- Names and tags are saved on the chest, so other players with the mod see them too.

### Requirements

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- Client-side. Other players and the server don't need it.

### Installation

**r2modman / Thunderstore Mod Manager:** click *Install*. To install a downloaded zip, go to Settings → *Import local mod*.

**Manual:**
1. Extract the zip.
2. Copy the `plugins/Hoardkeeper` folder into `Valheim/BepInEx/plugins/`.
   Result: `Valheim/BepInEx/plugins/Hoardkeeper/Hoardkeeper.dll`.
3. Launch the game.

To update, delete the old `Hoardkeeper` folder before copying the new one.

### Configuration

Press **F1** in game (requires Configuration Manager) or edit `BepInEx/config/felih.Hoardkeeper.cfg`.

| Option | Default | What it does |
|---|---|---|
| ChestRange | 50 | How far (metres) Storage, Stack and Restock reach for chests (5–50). |
| MaxChests | 60 | Most chests the Storage window shows, nearest first. |
| Storage / Stack / Restock | Shift+F / Shift+Q / Shift+R | Keys. |
| SortKey | T | Sorts the open chest or your bag; with Shift, changes the order. |
| Order | Type | Sort order. |
| AutoSortOnOpen / AutoSortOnPickup | off | Automatic sorting. |
| GatherFromBag | off | Gather also takes from your bag (also a button in the window). |
| WindowWidth / WindowHeight | 0 (automatic) | Size of the Storage window. |
| ScrollSpeed | 3000 | Mouse wheel speed in the Storage window. |
| ButtonsOffset | 8, 0 | Where the button column sits, from the top-right corner of the chest window. |

### Notes

- Hoardkeeper does the jobs of storage mods such as Storage Master and Quick Stack Store. Don't run it together with them, or keys and buttons will clash.

---

## Português

Todos os baús ao seu redor numa janela só. Organize, empilhe, reabasteça, marque com tags e dê nomes aos seus baús, e ache qualquer item da base sem abrir baú por baú.

### Recursos

**Janela Storage** (Shift + F, ou o botão *Storage* ao lado do inventário)
- Mostra todos os baús a até 50 m numa grade, dois ou mais lado a lado, com os próprios espaços de baú do jogo. Arrastar, dividir e Ctrl + clique funcionam como num baú normal.
- **Busca:** os itens que combinam acendem e o resto fica apagado. Os baús que têm o item sobem para o topo.
- **Clique direito num item** acende esse item em todos os baús e mostra quantos você tem no total. Esses baús também sobem para o topo.
- **Comandos à esquerda:**
  - *Sort all*: organiza todos.
  - *Tidy up*: junta cada item no baú que tem mais dele.
  - *Stack*
  - *Fill tags*
  - *Filter*: mostra só os baús com uma tag.
  - *Bag on/off*: se o Gather também pega da bolsa.
  - *Close*
- **Botões em cada baú:**
  - *Gather*: traz para cá o que os outros baús têm dos itens deste.
  - *Sort*
  - *Take all*
  - *Tags*
  - *Name*
  - *Skip*: esconde o baú da janela, do Stack e do Restock, só para você.
- Ctrl + clique num item da bolsa manda ele para um baú com a tag dele. Se não houver, vai para um baú que já tem esse item.
- Embaixo da bolsa, uma listinha mostra suas últimas movimentações com Ctrl + clique e para qual baú cada item foi.

**Tags de baú**
- Dê a um baú um ou mais tipos de item, pela janela Storage ou pela janela do próprio baú. São 20 tipos: armas, munição, armadura, ferramentas, comida, hidromel, carne, peixe, colheita, sementes, minério, metais, madeira, pedra, couro, partes de criatura, valiosos, troféus, materiais e diversos.
- **Fill tags** manda cada item para o baú marcado com o tipo dele, da bolsa e dos outros baús.
- **Fill tags na janela do próprio baú** enche só aquele baú.

**Nomes de baú**
- Dê nome a um baú pela janela Storage, ou pelo botão *Name* na janela do próprio baú.
- O nome aparece quando você olha para o baú e na janela dele.

**Bolsa e baús**
- **T** organiza o baú aberto, ou a bolsa. **Shift + T** (ou Shift + clique em *Sort*) troca a ordem: tipo, nome A–Z ou Z–A, qualidade, peso, quantidade ou valor.
- **Stack** (Shift + Q) guarda seus itens nos baús próximos que já têm esses itens.
- **Restock** (Shift + R) completa a barra rápida, a munição e os espaços travados com os baús próximos.
- Botão **Store all** e uma **lixeira**: solte um item nela e confirme para destruí-lo.
- Organização automática opcional ao abrir o inventário ou um baú, e depois de pegar algo.

**Seus itens ficam onde estão**
- **Alt + clique** trava um espaço da bolsa.
- **Alt + clique direito** marca um tipo de item como favorito, com um losango dourado.
- Nada move os itens da barra rápida, equipados, travados ou favoritos, nem os que estão em espaços extras de equipamento de outros mods.

**Joga limpo**
- Baús em uso por outro jogador, baús privados de outros jogadores e baús dentro de proteções que você não pode abrir ficam de fora.
- Nomes e tags ficam gravados no baú, então outros jogadores com o mod também veem.

### Requisitos

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- Só no cliente. Os outros jogadores e o servidor não precisam do mod.

### Instalação

**r2modman / Thunderstore Mod Manager:** clique em *Install*. Para instalar um zip baixado: Settings → *Import local mod*.

**Manual:**
1. Extraia o zip.
2. Copie a pasta `plugins/Hoardkeeper` para `Valheim/BepInEx/plugins/`.
   Resultado: `Valheim/BepInEx/plugins/Hoardkeeper/Hoardkeeper.dll`.
3. Abra o jogo.

Para atualizar, apague a pasta `Hoardkeeper` antiga antes de copiar a nova.

### Configuração

Aperte **F1** no jogo (precisa do Configuration Manager) ou edite `BepInEx/config/felih.Hoardkeeper.cfg`. As opções estão na tabela em inglês acima.

### Observações

- O Hoardkeeper faz o trabalho de mods de baú como Storage Master e Quick Stack Store. Não use junto com eles, senão teclas e botões entram em conflito.
