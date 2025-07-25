import requests
import time
from typing import List, Dict, Optional
from dataclasses import dataclass
import logging
import json
from reportlab.lib.pagesizes import A4
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.lib.units import inch
from reportlab.lib.colors import blue, black

# Configurar logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@dataclass
class GitBookPage:
    """Estrutura de uma página do GitBook"""
    id: str
    title: str
    slug: str
    path: str
    table: str
    parent_id: Optional[str]
    space_id: str
    space_title: str
    url: str
    content: str = ""

class GitBookContentExtractor:
    """Extrator de conteúdo do GitBook com geração de PDF"""
    
    def __init__(self, api_token: str):
        self.api_token = api_token
        self.base_url = "https://api.gitbook.com/v1"
        self.headers = {
            "Authorization": f"Bearer {api_token}",
            "Content-Type": "application/json"
        }
        self.session = requests.Session()
        self.session.headers.update(self.headers)
    
    def extract_text_from_nodes(self, nodes: List[Dict]) -> str:
        """
        Extrai texto dos nós do GitBook - VERSÃO CORRIGIDA
        """
        texts = []
        
        def extract_from_node(node):
            if not isinstance(node, dict):
                return []
            
            node_texts = []
            
            # Estratégia principal: buscar em 'leaves' que contêm o texto real
            if 'nodes' in node and isinstance(node['nodes'], list):
                for child_node in node['nodes']:
                    if isinstance(child_node, dict):
                        # Verificar se é um nó de texto com leaves
                        if child_node.get('object') == 'text' and 'leaves' in child_node:
                            leaves = child_node['leaves']
                            if isinstance(leaves, list):
                                for leaf in leaves:
                                    if isinstance(leaf, dict) and 'text' in leaf:
                                        text = leaf['text'].strip()
                                        if text:
                                            node_texts.append(text)
                        
                        # Recursão para nós aninhados (como list-items)
                        else:
                            nested_texts = extract_from_node(child_node)
                            node_texts.extend(nested_texts)
            
            return node_texts
        
        # Processar todos os nós principais
        for node in nodes:
            extracted_texts = extract_from_node(node)
            texts.extend(extracted_texts)
        
        # Juntar textos com espaços
        full_text = ' '.join(texts)
        
        # Limpeza básica
        full_text = ' '.join(full_text.split())  # Remove espaços extras
        
        return full_text
    
    def get_page_content(self, space_id: str, page_id: str) -> str:
        """Busca o conteúdo de uma página específica"""
        try:
            logger.info(f"Buscando conteúdo da página {page_id}")
            
            # Usar o endpoint que funciona
            endpoint = f"{self.base_url}/spaces/{space_id}/content/page/{page_id}"
            response = self.session.get(endpoint)
            
            if response.status_code == 200:
                data = response.json()
                
                # Extrair conteúdo usando o método corrigido
                if 'document' in data and 'nodes' in data['document']:
                    nodes = data['document']['nodes']
                    content = self.extract_text_from_nodes(nodes)
                    
                    logger.info(f"Conteúdo extraído: {len(content)} caracteres")
                    return content
                else:
                    logger.warning(f"Estrutura inesperada na resposta da página {page_id}")
                    return ""
            else:
                logger.error(f"Erro {response.status_code} ao buscar página {page_id}")
                return ""
                
        except Exception as e:
            logger.error(f"Erro ao buscar conteúdo da página {page_id}: {str(e)}")
            return ""
    
    def get_all_pages_from_space(self, space_id: str) -> List[GitBookPage]:
        """Extrai todas as páginas de um space COM CONTEÚDO"""
        try:
            logger.info(f"Buscando páginas do space: {space_id}")
            
            # Buscar info do space
            space_response = self.session.get(f"{self.base_url}/spaces/{space_id}")
            if space_response.status_code != 200:
                logger.error(f"Erro ao buscar space {space_id}: {space_response.status_code}")
                return []
            
            space_info = space_response.json()
            space_title = space_info.get('title', 'Sem título')
            
            logger.info(f"Processando space: '{space_title}'")
            
            # Buscar conteúdo do space
            content_response = self.session.get(f"{self.base_url}/spaces/{space_id}/content")
            if content_response.status_code != 200:
                logger.error(f"Erro ao buscar conteúdo: {content_response.status_code}")
                return []
            
            content_data = content_response.json()
            pages_data = content_data.get('pages', [])
            
            all_pages = []
            
            def process_pages(pages, parent_path="", parent_id=None):
                for page_item in pages:
                    page_type = page_item.get('type', 'unknown')
                    
                    # Só processar páginas reais (documents)
                    if page_type in ['document', 'page']:
                        page_id = page_item.get('id')
                        page_title = page_item.get('title', 'Sem título')
                        page_slug = page_item.get('slug', page_item.get('path', ''))
                        table = page_item.get('',page)
                        
                        current_path = f"{parent_path}/{page_slug}" if parent_path else page_slug
                        public_url = self._get_public_url(space_id, current_path, space_info)
                        logger.info(f"Processando página: {page_title}")
                        page_content = self.get_page_content(space_id, page_id)
                        
                        page_obj = GitBookPage(
                            id=page_id,
                            title=page_title,
                            slug=page_slug,
                            path=current_path,
                            parent_id=parent_id,
                            space_id=space_id,
                            space_title=space_title,
                            url=public_url,
                            content=page_content
                        )
                        all_pages.append(page_obj)
                        
                        time.sleep(0.2)
                    
                    # Processar sub-páginas
                    if 'pages' in page_item:
                        process_pages(page_item['pages'], current_path, page_item.get('id'))
            
            process_pages(pages_data)
            
            logger.info(f"✅ Processadas {len(all_pages)} páginas com conteúdo")
            return all_pages
            
        except Exception as e:
            logger.error(f"Erro ao processar space {space_id}: {str(e)}")
            return []
    
    def _get_public_url(self, space_id: str, path: str, space_info: Dict) -> str:
        """Constrói URL pública da página"""
        visibility = space_info.get('visibility', 'private')
        
        if visibility == 'public':
            base_url = space_info.get('urls', {}).get('public', f"https://app.gitbook.com/s/{space_id}")
            return f"{base_url}/{path}" if path else base_url
        else:
            return f"https://app.gitbook.com/s/{space_id}/{path}" if path else f"https://app.gitbook.com/s/{space_id}"
    
    def generate_pdf(self, pages: List[GitBookPage], filename: str = "gitbook_content.pdf"):
        try:
            logger.info(f"Gerando PDF: {filename}")
            
            # Configurar documento
            doc = SimpleDocTemplate(
                filename, 
                pagesize=A4,
                rightMargin=inch,
                leftMargin=inch,
                topMargin=inch,
                bottomMargin=inch
            )
            
            story = []
            styles = getSampleStyleSheet()
            
            # Criar estilos personalizados
            title_style = ParagraphStyle(
                'CustomTitle',
                parent=styles['Heading1'],
                fontSize=18,
                spaceAfter=20,
                textColor=blue,
                alignment=TA_CENTER
            )
            
            page_title_style = ParagraphStyle(
                'PageTitle',
                parent=styles['Heading2'],
                fontSize=14,
                spaceAfter=12,
                textColor=black,
                alignment=TA_LEFT
            )
            
            content_style = ParagraphStyle(
                'Content',
                parent=styles['Normal'],
                fontSize=11,
                spaceAfter=6,
                leading=14
            )
            
            # Título principal
            space_title = pages[0].space_title if pages else "GitBook Content"
            story.append(Paragraph(f"<b>{space_title}</b>", title_style))
            story.append(Spacer(1, 20))
            
            # Adicionar páginas
            for i, page in enumerate(pages):
                if page.content:
                    # Título da página
                    story.append(Paragraph(f"<b>{page.title}</b>", page_title_style))
                    story.append(Spacer(1, 12))
                    # URL da página
                    story.append(Paragraph(f"<i>URL: {page.url}</i>", styles['Normal']))
                    story.append(Spacer(1, 8))
                    # Conteúdo da página
                    # Dividir conteúdo em parágrafos para melhor formatação
                    content_paragraphs = page.content.split('\n\n')
                    for paragraph in content_paragraphs:
                        if paragraph.strip():
                            story.append(Paragraph(paragraph.strip(), content_style))
                    
                    # Quebra de página (exceto para a última página)
                    if i < len(pages) - 1:
                        story.append(PageBreak())
                else:
                    # Página sem conteúdo
                    story.append(Paragraph(f"<b>{page.title}</b>", page_title_style))
                    story.append(Paragraph("<i>Esta página não possui conteúdo.</i>", styles['Italic']))
                    story.append(Spacer(1, 20))
            
            # Construir PDF
            doc.build(story)
            logger.info(f"✅ PDF gerado com sucesso: {filename}")
            
        except Exception as e:
            logger.error(f"❌ Erro ao gerar PDF: {str(e)}")
            raise e
    
    def test_extraction(self, space_id: str):
        """Testa a extração com uma página específica"""
        logger.info("🧪 TESTANDO EXTRAÇÃO CORRIGIDA")
        
        pages = self.get_all_pages_from_space(space_id)
        
        if pages:
            logger.info(f"\n📊 RESULTADOS DO TESTE:")
            logger.info(f"Total de páginas processadas: {len(pages)}")
            
            for page in pages:
                content_preview = page.content[:100] if page.content else "SEM CONTEÚDO"
                logger.info(f"📄 {page.title}: {len(page.content)} chars - '{content_preview}...'")
        
        return pages

def main():
    """Função principal para testar"""
    
    # Configurações
    API_TOKEN = "gb_api_Itu4jxLl1CYfAAWsanBq8a5eR3Z16oiTKfhYUxEf"
    SPACE_ID = "lNWZwBFb2DiAy827Xuzc"
    
    print("🔧 TESTANDO EXTRATOR DO GITBOOK COM GERAÇÃO DE PDF")
    print("=" * 60)
    
    extractor = GitBookContentExtractor(API_TOKEN)
    pages = extractor.test_extraction(SPACE_ID)
    
    if pages:
        print(f"\n✅ SUCESSO! {len(pages)} páginas extraídas com conteúdo")
        print(f"\n📖 PRÉVIA DO CONTEÚDO EXTRAÍDO:")
        print("-" * 40)
        
        for page in pages:
            if page.content:
                print(f"\n🔖 {page.title}:")
                print(f"   📝 {page.content[:200]}...")
                print(f"   📊 Total: {len(page.content)} caracteres")
            else:
                print(f"\n⚠️ {page.title}: Sem conteúdo extraído")
        
        # Salvar resultado JSON para análise
        pages_data = []
        for page in pages:
            pages_data.append({
                "title": page.title,
                "url": page.url,
                "content_length": len(page.content),
                "content_preview": page.content[:500]
            })
        
        with open("extracted_content_test.json", "w", encoding="utf-8") as f:
            json.dump(pages_data, f, indent=2, ensure_ascii=False)
        
        print(f"\n💾 Resultado JSON salvo em: extracted_content_test.json")
        
        # GERAR PDF
        try:
            pdf_filename = f"gitbook_{SPACE_ID}_content.pdf"
            extractor.generate_pdf(pages, pdf_filename)
            print(f"\n📄 PDF gerado com sucesso: {pdf_filename}")
            print(f"🎉 EXTRAÇÃO E GERAÇÃO DE PDF CONCLUÍDA!")
        except Exception as e:
            print(f"\n❌ Erro ao gerar PDF: {str(e)}")
            print("💡 Certifique-se de instalar: pip install reportlab")
            
    else:
        print("❌ Nenhuma página foi extraída")

if __name__ == "__main__":
    main()