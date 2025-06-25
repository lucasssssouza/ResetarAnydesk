# ResetarAnydesk

**ResetarAnydesk** é um utilitário WinForms para Windows (baseado em .NET Framework 4.6) que automatiza o processo de “reset” do AnyDesk:

- Faz backup do arquivo de configuração (`user.conf`)  
- Encerra o AnyDesk  
- Desinstala silenciosamente a versão instalada  
- Limpa todas as pastas de configuração e instalação antigas  
- Roda o AnyDesk “limpo” para recriar o `user.conf`  
- Restaura o seu backup sobre o novo `user.conf`  
- Reinicia o AnyDesk com sua configuração original  
- Tudo empacotado em **um único `.exe`** com suporte a UAC (solicita privilégios de administrador)

---

## 📦 Principais recursos

- **Backup/Restore** automático do `user.conf`  
- **Uninstall silencioso** via Windows Registry  
- **Limpeza profunda** das pastas em `%AppData%`, `%ProgramData%` e `%ProgramFiles(x86)%`  
- **Publicação “single file”** com Fody-Costura (nenhuma DLL externa)  
- **Manifesto embutido** para solicitar permissões de administrador  
- Interface gráfica simples e responsiva (async/await)
