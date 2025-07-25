import requests
import json
from typing import Dict, Any

class GitBookContentDebugger:
    """Debug específico do space Api Core TMB Educação"""
    
    def __init__(self, api_token: str):
        self.api_token = api_token
        self.base_url = "https://api.gitbook.com/v1"
        self.headers = {
            "Authorization": f"Bearer {api_token}",
            "Content-Type": "application/json"
        }
        self.session = requests.Session()
        self.session.headers.update(self.headers)
    
    def debug_space_content(self, space_id: str):
        """Debug completo do conteúdo do space"""
        print(f"🔍 DEBUGANDO SPACE: {space_id}")
        print("Api Core TMB Educação")
        print("=" * 60)
        
        # 1. Informações do space
        self._debug_space_info(space_id)
        
        # 2. Estrutura de conteúdo
        content_data = self._debug_content_structure(space_id)
        
        # 3. Primeira página disponível
        if content_data:
            first_page = self._find_first_page(content_data)
            if first_page:
                self._debug_page_content(space_id, first_page)
    
    def _debug_space_info(self, space_id: str):
        """Debug das informações básicas do space"""
        print(f"\n📚 1. INFORMAÇÕES DO SPACE")
        print("-" * 30)
        
        try:
            response = self.session.get(f"{self.base_url}/spaces/{space_id}")
            
            if response.status_code == 200:
                space_data = response.json()
                print(f"✅ Space carregado com sucesso")
                print(f"   📛 Título: {space_data.get('title', 'N/A')}")
                print(f"   🔒 Visibilidade: {space_data.get('visibility', 'N/A')}")
                print(f"   🌐 URL Pública: {space_data.get('urls', {}).get('public', 'N/A')}")
                
                # Verificar permissões
                if 'permissions' in space_data:
                    perms = space_data['permissions']
                    print(f"   🔑 Permissões: {list(perms.keys()) if isinstance(perms, dict) else perms}")
                
            else:
                print(f"❌ Erro {response.status_code}: {response.text}")
                
        except Exception as e:
            print(f"❌ Erro: {str(e)}")
    
    def _debug_content_structure(self, space_id: str) -> Dict:
        """Debug da estrutura de conteúdo"""
        print(f"\n📋 2. ESTRUTURA DE CONTEÚDO")
        print("-" * 30)
        
        try:
            response = self.session.get(f"{self.base_url}/spaces/{space_id}/content")
            
            if response.status_code == 200:
                content_data = response.json()
                print(f"✅ Conteúdo carregado com sucesso")
                
                # Analisar estrutura
                self._analyze_content_structure(content_data)
                
                return content_data
            else:
                print(f"❌ Erro {response.status_code}: {response.text}")
                return {}
                
        except Exception as e:
            print(f"❌ Erro: {str(e)}")
            return {}
    
    def _analyze_content_structure(self, data: Dict, level: int = 0):
        """Analisa a estrutura do conteúdo"""
        indent = "  " * level
        
        if isinstance(data, dict):
            for key, value in data.items():
                if key == 'pages' and isinstance(value, list):
                    print(f"{indent}📄 {key}: {len(value)} páginas encontradas")
                    for i, page in enumerate(value[:3]):  # Mostrar apenas 3 primeiras
                        page_title = page.get('title', page.get('id', 'Sem título'))
                        page_type = page.get('type', 'unknown')
                        print(f"{indent}   {i+1}. {page_title} (tipo: {page_type})")
                    if len(value) > 3:
                        print(f"{indent}   ... e mais {len(value) - 3} páginas")
                
                elif key in ['id', 'title', 'type', 'slug']:
                    print(f"{indent}{key}: {value}")
    
    def _find_first_page(self, content_data: Dict) -> Dict:
        """Encontra a primeira página válida"""
        def search_pages(data):
            if isinstance(data, dict):
                # Procurar por páginas do tipo 'page' ou 'document'
                if data.get('type') in ['page', 'document'] and data.get('id'):
                    return data
                
                # Buscar recursivamente
                for key, value in data.items():
                    if key == 'pages' and isinstance(value, list):
                        for page in value:
                            result = search_pages(page)
                            if result:
                                return result
            
            return None
        
        return search_pages(content_data)
    
    def _debug_page_content(self, space_id: str, page_data: Dict):
        """Debug do conteúdo de uma página específica"""
        page_id = page_data.get('id')
        page_title = page_data.get('title', 'Sem título')
        
        print(f"\n📄 3. CONTEÚDO DA PÁGINA")
        print("-" * 30)
        print(f"🆔 ID: {page_id}")
        print(f"📛 Título: {page_title}")
        print(f"🏷️ Tipo: {page_data.get('type', 'N/A')}")
        
        # Testar diferentes endpoints para buscar conteúdo
        endpoints_to_test = [
            f"/spaces/{space_id}/content/page/{page_id}",
            f"/spaces/{space_id}/content/path/{page_data.get('slug', page_id)}",
            f"/spaces/{space_id}/pages/{page_id}",
            f"/content/{page_id}",
        ]
        
        successful_endpoints = []
        
        for endpoint in endpoints_to_test:
            print(f"\n🔗 Testando: {endpoint}")
            try:
                response = self.session.get(f"{self.base_url}{endpoint}")
                print(f"   Status: {response.status_code}")
                
                if response.status_code == 200:
                    data = response.json()
                    successful_endpoints.append((endpoint, data))
                    
                    # Analisar estrutura da resposta
                    print(f"   ✅ SUCESSO! Chaves encontradas:")
                    self._show_keys(data, level=2)
                    
                    # Tentar extrair texto
                    extracted_text = self._extract_text_from_response(data)
                    if extracted_text:
                        print(f"   📝 TEXTO EXTRAÍDO ({len(extracted_text)} chars):")
                        print(f"   {extracted_text[:200]}...")
                    else:
                        print(f"   ⚠️ Nenhum texto extraído")
                
                elif response.status_code == 404:
                    print(f"   ❌ Não encontrado")
                elif response.status_code == 403:
                    print(f"   🔒 Acesso negado")
                else:
                    print(f"   ⚠️ Erro: {response.text[:100]}...")
                    
            except Exception as e:
                print(f"   ❌ Erro: {str(e)}")
        
        # Salvar dados bem-sucedidos para análise
        if successful_endpoints:
            self._save_debug_data(page_id, successful_endpoints)
    
    def _show_keys(self, data: Any, level: int = 0):
        """Mostra as chaves de um objeto de forma hierárquica"""
        indent = "     " * level
        
        if isinstance(data, dict):
            for key, value in data.items():
                if isinstance(value, dict):
                    print(f"{indent}📁 {key}: dict com {len(value)} chaves")
                elif isinstance(value, list):
                    print(f"{indent}📦 {key}: lista com {len(value)} itens")
                else:
                    value_preview = str(value)[:50] if value else "vazio"
                    print(f"{indent}🔑 {key}: {type(value).__name__} = {value_preview}")
    
    def _extract_text_from_response(self, data: Dict) -> str:
        """Tenta extrair texto de diferentes estruturas de resposta"""
        texts = []
        
        # Estratégias de extração
        extraction_paths = [
            ['document', 'nodes'],
            ['content', 'nodes'],
            ['body', 'nodes'],
            ['nodes'],
            ['content'],
            ['text'],
            ['markdown'],
            ['plainText']
        ]
        
        for path in extraction_paths:
            current = data
            try:
                for key in path:
                    if isinstance(current, dict) and key in current:
                        current = current[key]
                    else:
                        break
                else:
                    # Chegou ao final do caminho
                    extracted = self._extract_text_recursive(current)
                    if extracted:
                        texts.append(f"[{' -> '.join(path)}]: {extracted}")
            except:
                continue
        
        return " | ".join(texts) if texts else ""
    
    def _extract_text_recursive(self, data: Any) -> str:
        """Extrai texto recursivamente de qualquer estrutura"""
        if isinstance(data, str):
            return data.strip()
        
        elif isinstance(data, dict):
            # Procurar por campos de texto conhecidos
            for text_field in ['text', 'content', 'value', 'plainText']:
                if text_field in data and isinstance(data[text_field], str):
                    return data[text_field].strip()
            
            # Buscar recursivamente
            for value in data.values():
                result = self._extract_text_recursive(value)
                if result:
                    return result
        
        elif isinstance(data, list):
            texts = []
            for item in data:
                result = self._extract_text_recursive(item)
                if result:
                    texts.append(result)
            return " ".join(texts)
        
        return ""
    
    def _save_debug_data(self, page_id: str, successful_endpoints: list):
        """Salva dados de debug para análise posterior"""
        debug_data = {
            "page_id": page_id,
            "successful_endpoints": {}
        }
        
        for endpoint, data in successful_endpoints:
            debug_data["successful_endpoints"][endpoint] = data
        
        filename = f"debug_page_{page_id}.json"
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(debug_data, f, indent=2, ensure_ascii=False)
        
        print(f"💾 Dados de debug salvos em: {filename}")

def main():
    """Função principal"""
    
    # Configurações
    API_TOKEN = "gb_api_Itu4jxLl1CYfAAWsanBq8a5eR3Z16oiTKfhYUxEf"
    SPACE_ID = "lNWZwBFb2DiAy827Xuzc"  # Api Core TMB Educação
    
    debugger = GitBookContentDebugger(API_TOKEN)
    debugger.debug_space_content(SPACE_ID)

if __name__ == "__main__":
    main()