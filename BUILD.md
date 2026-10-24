# Como compilar o AutoItemPickup 2.3.1

## Pré-requisitos
1. **Risk of Rain 2** instalado e atualizado (Steam).
2. **BepInEx** + **HookGenPatcher** instalados (pelo r2modman/Thunderstore, ou manualmente).
3. **Rode o jogo pelo menos uma vez** com eles instalados. É isso que gera o `MMHOOK_RoR2.dll`
   (normalmente em `BepInEx\plugins\MMHOOK\`). Sem esse arquivo não há como compilar os hooks `On.` / `IL.`.
4. **.NET SDK 6 ou superior** (`dotnet --version`) ou Visual Studio 2022 com a carga de trabalho ".NET desktop".

## Compilando (linha de comando)
Na raiz do projeto, onde está o `RoR2Mods.sln`:

    dotnet build -c Release -p:RoR2Dir="C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2"

Se você usa o r2modman, aponte para o perfil dele (o `BepInEx\core` e o `MMHOOK` ficam dentro do perfil):

    dotnet build -c Release ^
      -p:RoR2Dir="C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2" ^
      -p:BepInExCoreDir="%APPDATA%\r2modmanPlus-local\RiskOfRain2\profiles\Default\BepInEx\core" ^
      -p:MMHookDir="%APPDATA%\r2modmanPlus-local\RiskOfRain2\profiles\Default\BepInEx\plugins\MMHOOK"

(Confira os caminhos reais no seu PC; se `MMHOOK_RoR2.dll` estiver em outra pasta, ajuste `MMHookDir`.)

O build verifica se `RoR2.dll`, `BepInEx.dll` e `MMHOOK_RoR2.dll` existem e diz qual pasta está errada.

## Compilando (Visual Studio)
Abra `RoR2Mods.sln`, selecione **Release** e use **Build > Build Solution**. Se o jogo não estiver no caminho padrão,
defina a propriedade `RoR2Dir` (e, se for o caso, `BepInExCoreDir` e `MMHookDir`) em *Project > Properties*, ou
edite o bloco `PropertyGroup` no início do `AutoItemPickup.csproj`.

## Instalando
O resultado fica em `AutoItemPickup-Updated\bin\Release\netstandard2.1\AutoItemPickup.dll`.
Copie para `BepInEx\plugins\AutoItemPickup\` (ou para a pasta do mod no perfil do r2modman).

## Se algo quebrar depois de uma atualização do jogo
Abra `BepInEx\LogOutput.log` e procure por linhas `[AutoItemPickup]`:
- `Failed to register hooks` -> um método/campo do jogo sumiu ou mudou de nome (o nome do handler aparece no início da linha).
- `Could not find ... in <Método>` -> o método ainda existe, mas o IL mudou; esse patch específico foi pulado.
Os outros recursos continuam funcionando. Se aparecerem erros de compilação, o nome do método/campo citado é o que mudou
na `RoR2.dll`: abra-a no dnSpy/ILSpy e compare com o handler correspondente em `Hooks\`.
