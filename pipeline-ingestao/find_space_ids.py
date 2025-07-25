import requests
import json
from typing import List, Dict

class GitBookSpaceFinder:
    """Encontra todos os spaces e seus IDs no GitBook"""
    
    def __init__(self, api_token: str):
        self.api_token = api_token
        self.base_url = "https://api.gitbook.com/v1"
        self.headers = {
            "Authorization": f"Bearer {api_token}",
            "Content-Type": "application/json"
        }
        self.session = requests.Session()
        self.session.headers.update(self.headers)
    
    def test_connection(self) -> bool:
        """Testa a conexão e mostra info do usuário"""
        try:
            print(f"🔐 Testando conexão...")
            response = self.session.get(f"{self.base_url}/user")
            
            if response.status_code == 200:
                user_info = response.json()
                print(f"✅ Conectado como: {user_info.get('displayName', 'N/A')}")
                print(f"📧 Email: {user_info.get('email', 'N/A')}")
                return True
            else:
                print(f"❌ Erro na conexão: {response.status_code}")
                print(f"Resposta: {response.text}")
                return False
        except Exception as e:
            print(f"❌ Erro: {str(e)}")
            return False
    
    def get_all_organizations(self) -> List[Dict]:
        """Lista todas as organizações"""
        try:
            print(f"\n🏢 Buscando organizações...")
            response = self.session.get(f"{self.base_url}/orgs")
            
            if response.status_code == 200:
                data = response.json()
                orgs = data.get('items', [])
                print(f"✅ Encontradas {len(orgs)} organizações")
                return orgs
            else:
                print(f"⚠️ Status {response.status_code} ao buscar organizações")
                return []
        except Exception as e:
            print(f"❌ Erro ao buscar organizações: {str(e)}")
            return []
    
    def get_spaces_from_org(self, org_id: str, org_name: str) -> List[Dict]:
        """Lista spaces de uma organização específica"""
        try:
            print(f"\n📚 Buscando spaces da organização: {org_name}")
            response = self.session.get(f"{self.base_url}/orgs/{org_id}/spaces")
            
            if response.status_code == 200:
                data = response.json()
                spaces = data.get('items', [])
                print(f"   ✅ Encontrados {len(spaces)} spaces")
                return spaces
            else:
                print(f"   ⚠️ Status {response.status_code}")
                return []
        except Exception as e:
            print(f"   ❌ Erro: {str(e)}")
            return []
    
    def get_personal_spaces(self) -> List[Dict]:
        """Lista spaces pessoais (fora de organizações)"""
        try:
            print(f"\n👤 Buscando spaces pessoais...")
            response = self.session.get(f"{self.base_url}/spaces")
            
            if response.status_code == 200:
                data = response.json()
                spaces = data.get('items', [])
                print(f"   ✅ Encontrados {len(spaces)} spaces pessoais")
                return spaces
            else:
                print(f"   ⚠️ Status {response.status_code}")
                return []
        except Exception as e:
            print(f"   ❌ Erro: {str(e)}")
            return []
    
    def display_space_info(self, space: Dict, org_name: str = "Pessoal"):
        """Exibe informações detalhadas de um space"""
        space_id = space.get('id', 'N/A')
        title = space.get('title', 'Sem título')
        description = space.get('description', 'Sem descrição')
        visibility = space.get('visibility', 'N/A')
        
        # URLs
        urls = space.get('urls', {})
        public_url = urls.get('public', 'N/A')
        app_url = urls.get('app', 'N/A')
        
        print(f"\n📖 SPACE ENCONTRADO:")
        print(f"   🆔 ID: {space_id}")
        print(f"   📛 Nome: {title}")
        print(f"   🏢 Organização: {org_name}")
        print(f"   📝 Descrição: {description}")
        print(f"   🔒 Visibilidade: {visibility}")
        print(f"   🌐 URL Pública: {public_url}")
        print(f"   🔗 URL App: {app_url}")
        print(f"   " + "-" * 50)
        
        return space_id
    
    def find_all_spaces(self) -> Dict[str, str]:
        """Encontra todos os spaces e retorna dicionário {nome: id}"""
        all_spaces = {}
        
        print("🚀 INICIANDO BUSCA POR TODOS OS SPACES...")
        print("=" * 60)
        
        # 1. Buscar spaces pessoais
        personal_spaces = self.get_personal_spaces()
        for space in personal_spaces:
            space_id = self.display_space_info(space, "👤 Pessoal")
            space_name = space.get('title', f'Space_{space_id}')
            all_spaces[space_name] = space_id
        
        # 2. Buscar organizações e seus spaces
        orgs = self.get_all_organizations()
        for org in orgs:
            org_id = org.get('id')
            org_name = org.get('title', 'Organização sem nome')
            
            spaces = self.get_spaces_from_org(org_id, org_name)
            for space in spaces:
                space_id = self.display_space_info(space, f"🏢 {org_name}")
                space_name = space.get('title', f'Space_{space_id}')
                all_spaces[f"{org_name} - {space_name}"] = space_id
        
        return all_spaces
    
    def save_spaces_to_file(self, spaces: Dict[str, str]):
        """Salva lista de spaces em arquivo"""
        filename = "gitbook_spaces.json"
        
        spaces_list = []
        for name, space_id in spaces.items():
            spaces_list.append({
                "name": name,
                "id": space_id
            })
        
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(spaces_list, f, indent=2, ensure_ascii=False)
        
        print(f"\n💾 Lista de spaces salva em: {filename}")
    
    def create_debug_script(self, spaces: Dict[str, str]):
        """Cria script de debug personalizado com os space IDs encontrados"""
        
        if not spaces:
            print("❌ Nenhum space encontrado para criar script de debug")
            return
        
        # Pegar o primeiro space como exemplo
        first_space_name, first_space_id = list(spaces.items())[0]
        
        debug_script = f'''#!/usr/bin/env python3
# Script de debug gerado automaticamente
# {len(spaces)} spaces encontrados

import requests
import json

API_TOKEN = "{self.api_token}"
SPACES = {json.dumps(spaces, indent=4)}

# SPACE SELECIONADO PARA DEBUG (você pode mudar)
SELECTED_SPACE_ID = "{first_space_id}"  # {first_space_name}

print("🎯 Spaces disponíveis:")
for name, space_id in SPACES.items():
    print(f"   {space_id} → {name}")

print(f"\\n🔍 Debugando space: {first_space_name}")
print(f"ID: {first_space_id}")

# Aqui você pode adicionar o código de debug...
'''
        
        with open("debug_selected_space.py", "w", encoding="utf-8") as f:
            f.write(debug_script)
        
        print(f"🛠️ Script de debug criado: debug_selected_space.py")
        print(f"🎯 Space pré-selecionado: {first_space_name}")

def main():
    """Função principal"""
    
    # SEU TOKEN DO GITBOOK
    API_TOKEN = "gb_api_Itu4jxLl1CYfAAWsanBq8a5eR3Z16oiTKfhYUxEf"
    
    print("🔍 GITBOOK SPACE ID FINDER")
    print("=" * 50)
    
    finder = GitBookSpaceFinder(API_TOKEN)
    
    # Testar conexão
    if not finder.test_connection():
        print("❌ Falha na conexão. Verifique seu token.")
        return
    
    # Encontrar todos os spaces
    all_spaces = finder.find_all_spaces()
    
    if all_spaces:
        print(f"\n🎉 RESUMO: {len(all_spaces)} spaces encontrados!")
        print("\n📋 LISTA COMPLETA:")
        for name, space_id in all_spaces.items():
            print(f"   🆔 {space_id} → 📖 {name}")
        
        # Salvar em arquivo
        finder.save_spaces_to_file(all_spaces)
        
        # Criar script de debug
        finder.create_debug_script(all_spaces)
        
        print(f"\n✅ PRÓXIMOS PASSOS:")
        print(f"1. Escolha um Space ID da lista acima")
        print(f"2. Use esse ID no código de debug")
        print(f"3. Ou execute: python debug_selected_space.py")
        
    else:
        print("❌ Nenhum space encontrado. Verifique suas permissões.")

if __name__ == "__main__":
    main()