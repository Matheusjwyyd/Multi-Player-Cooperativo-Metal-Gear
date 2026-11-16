# Revisão estática do Dynamite 0.6.0

## Escopo e resultado

Foi analisada a cópia `dynamite-0.6.0` fornecida em Downloads sem executar o programa, compilar o projeto ou instalar arquivos no MGSV. A cópia contém 173 arquivos regulares, totalizando 6.890.141 bytes.

Comparei o SHA-256 de cada arquivo com o tag oficial `v0.6.0` do repositório [`unknown321/dynamite`](https://github.com/unknown321/dynamite/releases/tag/v0.6.0). Os 173 arquivos são idênticos ao commit `7fe74d31128ed8d3741b07181cb99e4adc1f57be`: não há arquivos ausentes, extras ou modificados em relação a esse tag.

Não há `.exe`, `.dll`, `.sys`, `.msi`, scripts `.bat`/`.ps1` ou arquivos com assinatura PE/MZ na cópia. Ela é código-fonte e recursos: Go, C++, headers, Lua, documentação, JSON e imagens PNG. O projeto fonte pode gerar um instalador e `dinput8.dll` quando compilado; esses binários não estão nesta pasta.

**Não encontrei indícios de malware na análise estática.** Isso não é uma garantia de ausência de vulnerabilidades e não substitui uma varredura antivírus de binários compilados. Nenhum arquivo foi executado.

## Comportamentos encontrados

- O instalador cria backups dos saves Steam, converte/copias saves para nomes próprios do mod, extrai e reempacota `master/0/00.dat`, grava scripts Lua e instala `dinput8.dll`. São alterações esperadas para instalar um mod, mas têm impacto real nos dados do jogo e nos saves.
- O código Go que prepara o executável tem rotinas para restaurar `mgsvtpp.exe` a partir de `mgsvtpp.exe.vendor` e aplicar patches. Nesta cópia, chamadas importantes como `PatchAll`, `UpdateURL` e `WriteCoopTxt` aparecem desativadas; isso reforça que o projeto tem trechos em desenvolvimento.
- O código nativo usa um proxy de DirectInput, hooks e alteração de proteção de memória do próprio jogo para carregar o mod e modificar funções do FOX Engine. Isso é compatível com a finalidade declarada do Dynamite; também significa que o DLL compilado tem poder para interferir no processo do jogo.
- As chamadas externas explícitas no código são a consulta de atualizações ao GitHub e a resolução de perfis Steam. O master server local usa `127.0.0.1:6667` por padrão. Não encontrei rotina de persistência do Windows, envio de arquivos pessoais ou execução de comandos remotos.
- `exec.Command` aparece para abrir a interface local no navegador (`cmd.exe /c start`) e consultar `uname` em WSL. Não encontrei chamadas para criar processos remotos ou injetar em outros processos.
- A interface web do mod usa HTTP sem autenticação e, por padrão, escuta em `127.0.0.1:8070`. Ela tem rotas para salvar configuração, parar o serviço e iniciar a instalação. Se a configuração for alterada para escutar em todas as interfaces, outra máquina da rede poderá controlar essa interface; mantenha-a no loopback.
- Os scripts Lua leem configuração local, escrevem log local e carregam módulos configurados para atalhos. O `loadstring` encontrado converte nomes de teclas/configuração em constantes Lua. Não encontrei acesso de rede ou execução de shell nos scripts Lua.
- O `Makefile`/CMake baixa dependências de repositórios Git durante a compilação. Os commits estão fixados no CMake; ainda assim, compilar requer confiar nessas dependências e na cadeia de ferramentas.

## Compatibilidade e uso como referência

Esta cópia tem a tabela `mgsvtpp_addresses_1_0_15_3_en.h` e offsets associados à build 1.0.15.3. A auditoria deste projeto registra a instalação do jogo em 1.0.15.4; não há tabela 1.0.15.4 neste tag. Portanto, não instale este Dynamite 0.6.0 na instalação atual: hooks com endereços de outra build podem travar o jogo ou alterar memória errada. A página do projeto também documenta a compatibilidade e os avisos da versão publicada: [repositório upstream](https://github.com/unknown321/dynamite).

Como referência, o projeto demonstra uma rota nativa de campanha: usa sessão FOB/Steam P2P, reserva uma segunda instância de jogador via definição FOX, altera scripts de missões e usa hooks nativos. Isso ajuda a orientar o Phantom Brothers; o servidor WebSocket do Phantom Brothers não substitui essa integração dentro do jogo.

O Dynamite é licenciado sob GPLv3 com Commons Clause, conforme o [LICENSE do tag v0.6.0](https://github.com/unknown321/dynamite/blob/v0.6.0/LICENSE). Não copie arquivos ou código diretamente para o Phantom Brothers sem decidir como cumprir essa licença e registrar os avisos correspondentes. Esta revisão não incorpora código do Dynamite.

## Conclusão prática

A cópia examinada corresponde byte a byte ao tag oficial de código-fonte e não contém um executável pré-compilado. É uma referência útil de arquitetura, mas não é uma build compatível comprovada com a versão atual do jogo nem um pacote seguro para instalar sem backup e validação específica.
