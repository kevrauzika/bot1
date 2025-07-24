import requests
import json
import time
import os
from typing import Dict, List, Optional
from dataclasses import dataclass
from datetime import datetime
import tempfile

@dataclass
class GitBookPage:
    """Estrutura de uma página do GitBook"""
    id: str
    title: str
    slug: str
    path: str
    parent_id: Optional[str]
    space_id: str
    space_title: str
    url: str
    pdf_path: Optional[str] = None

class SimpleGitBookToPDFProcessor:
    """Processa GitBook criando PDFs com informações básicas das páginas"""
    
    def __init__(self, api_token: str, output_dir: str = "gitbook_pdfs"):
        self.api_token = api_token
        self.base_url = "https://api.gitbook.com/v1"
        self.output_dir = output_dir
        self.headers = {
            "Authorization": f"Bearer {api_token}",
            "Content-Type": "application/json"
        }
        self.session = requests.Session()
        self.session.headers.update(self.headers)
        
        # Criar diretório de output
        os.makedirs(output_dir, exist_ok=True)
        
        print(f"📁 PDFs serão salvos em: {os.path.abspath(output_dir)}")
    
    def test_connection(self) -> bool:
        """Testa conexão com a API"""
        try:
            print(f"🔐 Testando token: {self.api_token[:8]}...")
            
            response = self.session.get(f"{self.base_url}/user")
            print(f"📊 Status da requisição: {response.status_code}")
            print(f"📋 Headers de resposta: {dict(response.headers)}")
            
            if response.status_code == 200:
                user_info = response.json()
                print(f"✅ Conectado como: {user_info.get('displayName', 'Unknown')}")
                return True
            elif response.status_code == 401:
                print(f"🔒 Erro 401 - Token inválido ou expirado")
                print(f"📝 Resposta: {response.text}")
                print(f"\n💡 SOLUÇÕES:")
                print(f"1. Verifique se o token está correto")
                print(f"2. Confirme se o token não expirou")
                print(f"3. Verifique se tem permissões necessárias")
                return False
            else:
                print(f"❌ Erro na conexão: {response.status_code}")
                print(f"📝 Resposta: {response.text}")
                return False
        except Exception as e:
            print(f"❌ Erro na conexão: {str(e)}")
            return False
    
    def get_organizations(self) -> List[Dict]:
        """Lista organizações"""
        try:
            response = self.session.get(f"{self.base_url}/orgs")
            response.raise_for_status()
            orgs = response.json().get('items', [])
            print(f"📋 Encontradas {len(orgs)} organizações")
            return orgs
        except Exception as e:
            print(f"❌ Erro ao buscar organizações: {str(e)}")
            return []
    
    def get_spaces(self, org_id: Optional[str] = None) -> List[Dict]:
        """Lista spaces de uma organização"""
        try:
            endpoint = f"{self.base_url}/orgs/{org_id}/spaces" if org_id else f"{self.base_url}/spaces"
            response = self.session.get(endpoint)
            
            if response.status_code == 200:
                spaces = response.json().get('items', [])
                print(f"📚 Encontrados {len(spaces)} spaces")
                return spaces
            else:
                print(f"⚠️ Status {response.status_code} para spaces")
                return []
        except Exception as e:
            print(f"❌ Erro ao buscar spaces: {str(e)}")
            return []
    
    def get_all_pages_from_space(self, space_id: str) -> List[GitBookPage]:
        """Extrai todas as páginas de um space"""
        try:
            print(f"🔍 Buscando páginas do space: {space_id}")
            
            # Buscar info do space
            space_response = self.session.get(f"{self.base_url}/spaces/{space_id}")
            if space_response.status_code != 200:
                print(f"❌ Erro ao buscar space {space_id}: {space_response.status_code}")
                return []
            
            space_info = space_response.json()
            space_title = space_info.get('title', 'Sem título')
            
            print(f"📖 Processando space: '{space_title}'")
            
            # Buscar conteúdo do space
            content_response = self.session.get(f"{self.base_url}/spaces/{space_id}/content")
            if content_response.status_code != 200:
                print(f"❌ Erro ao buscar conteúdo do space: {content_response.status_code}")
                return []
            
            content_data = content_response.json()
            pages_data = content_data.get('pages', [])
            
            all_pages = []
            
            def process_pages(pages, parent_path="", parent_id=None):
                for page_item in pages:
                    page_type = page_item.get('type', 'unknown')
                    
                    if page_type in ['document', 'page']:  # Só processar páginas reais
                        page_id = page_item.get('id')
                        page_title = page_item.get('title', 'Sem título')
                        page_slug = page_item.get('slug', page_item.get('path', ''))
                        
                        # Construir path hierárquico
                        current_path = f"{parent_path}/{page_slug}" if parent_path else page_slug
                        
                        # URL pública da página
                        public_url = self._get_public_url(space_id, current_path, space_info)
                        
                        page_obj = GitBookPage(
                            id=page_id,
                            title=page_title,
                            slug=page_slug,
                            path=current_path,
                            parent_id=parent_id,
                            space_id=space_id,
                            space_title=space_title,
                            url=public_url
                        )
                        all_pages.append(page_obj)
                        print(f"  📄 Encontrada: {page_title}")
                    
                    # Processar sub-páginas
                    if 'pages' in page_item:
                        process_pages(page_item['pages'], current_path, page_item.get('id'))
            
            process_pages(pages_data)
            
            print(f"✅ Encontradas {len(all_pages)} páginas no space '{space_title}'")
            return all_pages
            
        except Exception as e:
            print(f"❌ Erro ao processar space {space_id}: {str(e)}")
            return []
    
    def _get_public_url(self, space_id: str, path: str, space_info: Dict) -> str:
        """Constrói URL pública da página"""
        # Verificar se o space é público
        visibility = space_info.get('visibility', 'private')
        
        if visibility == 'public':
            # Para spaces públicos, usar URL direta
            base_url = space_info.get('urls', {}).get('public', f"https://app.gitbook.com/s/{space_id}")
            return f"{base_url}/{path}" if path else base_url
        else:
            # Para spaces privados, usar URL da app
            return f"https://app.gitbook.com/s/{space_id}/{path}" if path else f"https://app.gitbook.com/s/{space_id}"
    
    def create_page_pdf(self, page: GitBookPage) -> bool:
        """Cria PDF com informações da página usando ReportLab"""
        try:
            print(f"📄 Criando PDF: {page.title}")
            
            # Nome do arquivo PDF (limitar tamanho e caracteres especiais)
            safe_filename = self._sanitize_filename(f"{page.space_title}_{page.title}")
            pdf_filename = f"{safe_filename}.pdf"
            pdf_path = os.path.join(self.output_dir, pdf_filename)
            
            # Se já existe, pular
            if os.path.exists(pdf_path):
                print(f"  ⚠️ PDF já existe: {pdf_filename}")
                page.pdf_path = pdf_path
                return True
            
            # Tentar extrair algum conteúdo da página
            page_content = self._get_page_info(page)
            
            # Criar PDF com ReportLab
            success = self._create_pdf_with_reportlab(page, page_content, pdf_path)
            
            if success:
                page.pdf_path = pdf_path
                print(f"  ✅ PDF criado: {pdf_filename}")
                return True
            else:
                print(f"  ❌ Falha ao criar PDF para: {page.title}")
                return False
                
        except Exception as e:
            print(f"❌ Erro ao criar PDF {page.title}: {str(e)}")
            return False
    
    def _get_page_info(self, page: GitBookPage) -> str:
        """Tenta extrair informações básicas da página"""
        try:
            # Tentar buscar informações básicas da página
            page_response = self.session.get(f"{self.base_url}/spaces/{page.space_id}/content/page/{page.id}")
            
            if page_response.status_code == 200:
                page_data = page_response.json()
                
                # Tentar extrair algum texto dos nós
                document = page_data.get('document', {})
                nodes = document.get('nodes', [])
                
                text_content = []
                for node in nodes[:5]:  # Pegar apenas os primeiros 5 nós
                    if node.get('type') == 'paragraph':
                        node_nodes = node.get('nodes', [])
                        for text_node in node_nodes:
                            if text_node.get('object') == 'text':
                                leaves = text_node.get('leaves', [])
                                for leaf in leaves:
                                    text = leaf.get('text', '')
                                    if text.strip():
                                        text_content.append(text.strip())
                
                if text_content:
                    return ' '.join(text_content)[:1000]  # Limitar a 1000 caracteres
            
            return "Conteúdo não disponível via API. Acesse a URL para visualizar o conteúdo completo."
            
        except Exception as e:
            print(f"    ⚠️ Erro ao buscar conteúdo: {str(e)}")
            return "Erro ao extrair conteúdo da página."
    
    def _create_pdf_with_reportlab(self, page: GitBookPage, content: str, pdf_path: str) -> bool:
        """Cria PDF usando ReportLab"""
        try:
            from reportlab.lib.pagesizes import letter, A4
            from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle
            from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
            from reportlab.lib.units import inch
            from reportlab.lib import colors
            
            # Criar documento PDF
            doc = SimpleDocTemplate(pdf_path, pagesize=A4, 
                                  rightMargin=72, leftMargin=72, 
                                  topMargin=72, bottomMargin=72)
            
            styles = getSampleStyleSheet()
            
            # Criar estilos customizados
            title_style = ParagraphStyle(
                'CustomTitle',
                parent=styles['Heading1'],
                fontSize=20,
                spaceAfter=20,
                textColor=colors.HexColor('#2c3e50'),
                alignment=1  # Centralizado
            )
            
            subtitle_style = ParagraphStyle(
                'CustomSubtitle',
                parent=styles['Heading2'],
                fontSize=14,
                spaceAfter=10,
                textColor=colors.HexColor('#34495e')
            )
            
            content_style = ParagraphStyle(
                'CustomContent',
                parent=styles['Normal'],
                fontSize=11,
                spaceAfter=10,
                leading=14
            )
            
            story = []
            
            # Cabeçalho
            story.append(Paragraph(page.title, title_style))
            story.append(Spacer(1, 20))
            
            # Informações da página em tabela
            info_data = [
                ['Space:', page.space_title],
                ['Caminho:', page.path],
                ['URL:', page.url],
                ['Gerado em:', datetime.now().strftime('%d/%m/%Y às %H:%M:%S')]
            ]
            
            info_table = Table(info_data, colWidths=[1.5*inch, 4*inch])
            info_table.setStyle(TableStyle([
                ('BACKGROUND', (0, 0), (0, -1), colors.HexColor('#ecf0f1')),
                ('TEXTCOLOR', (0, 0), (0, -1), colors.HexColor('#2c3e50')),
                ('FONTNAME', (0, 0), (0, -1), 'Helvetica-Bold'),
                ('FONTSIZE', (0, 0), (-1, -1), 10),
                ('GRID', (0, 0), (-1, -1), 1, colors.HexColor('#bdc3c7')),
                ('VALIGN', (0, 0), (-1, -1), 'TOP'),
                ('LEFTPADDING', (0, 0), (-1, -1), 6),
                ('RIGHTPADDING', (0, 0), (-1, -1), 6),
                ('TOPPADDING', (0, 0), (-1, -1), 6),
                ('BOTTOMPADDING', (0, 0), (-1, -1), 6),
            ]))
            
            story.append(info_table)
            story.append(Spacer(1, 30))
            
            # Conteúdo da página
            story.append(Paragraph("Prévia do Conteúdo:", subtitle_style))
            story.append(Spacer(1, 10))
            
            # Adicionar conteúdo em parágrafos
            if content and len(content.strip()) > 0:
                # Dividir conteúdo em parágrafos menores
                paragraphs = content.split('. ')
                for para in paragraphs:
                    if para.strip():
                        para_clean = para.strip()
                        if not para_clean.endswith('.'):
                            para_clean += '.'
                        story.append(Paragraph(para_clean, content_style))
            else:
                story.append(Paragraph(
                    "O conteúdo desta página não pôde ser extraído automaticamente via API.",
                    content_style
                ))
            
            story.append(Spacer(1, 20))
            
            # Nota final
            story.append(Paragraph(
                "<b>Nota:</b> Para acessar o conteúdo completo, mais atualizado e com formatação original, "
                "visite a URL indicada acima no GitBook.",
                content_style
            ))
            
            # Gerar PDF
            doc.build(story)
            return True
            
        except Exception as e:
            print(f"    ❌ Erro ao criar PDF com ReportLab: {str(e)}")
            return False
    
    def _sanitize_filename(self, filename: str) -> str:
        """Limpa nome do arquivo para ser seguro no sistema de arquivos"""
        import re
        # Remove caracteres especiais e substitui espaços
        filename = re.sub(r'[<>:"/\\|?*]', '', filename)
        filename = re.sub(r'\s+', '_', filename)
        # Remove acentos
        import unicodedata
        filename = unicodedata.normalize('NFKD', filename).encode('ascii', 'ignore').decode('ascii')
        # Limita o tamanho
        return filename[:80]
    
    def process_all_spaces(self) -> List[GitBookPage]:
        """Processa todos os spaces e cria PDFs"""
        print("🚀 Iniciando conversão GitBook → PDF...")
        
        all_pages = []
        
        # Buscar organizações
        orgs = self.get_organizations()
        
        for org in orgs:
            org_id = org.get('id')
            org_name = org.get('title', 'Sem nome')
            print(f"\n🏢 Processando organização: {org_name}")
            
            # Buscar spaces da organização
            spaces = self.get_spaces(org_id)
            
            for space in spaces:
                space_id = space.get('id')
                if not space_id:
                    continue
                
                print(f"\n📚 Processando space: {space.get('title', 'Sem título')}")
                
                # Buscar páginas do space
                pages = self.get_all_pages_from_space(space_id)
                
                # Criar PDF para cada página
                for page in pages:
                    if self.create_page_pdf(page):
                        all_pages.append(page)
                    
                    # Rate limiting
                    time.sleep(0.5)
        
        print(f"\n🎉 CONCLUÍDO! {len(all_pages)} PDFs gerados em: {self.output_dir}")
        return all_pages
    
    def generate_summary_report(self, pages: List[GitBookPage]) -> str:
        """Gera relatório resumo da conversão"""
        report_path = os.path.join(self.output_dir, "conversion_report.txt")
        
        with open(report_path, 'w', encoding='utf-8') as f:
            f.write("RELATÓRIO DE CONVERSÃO GITBOOK → PDF\n")
            f.write("="*50 + "\n\n")
            f.write(f"Data da conversão: {datetime.now().strftime('%d/%m/%Y às %H:%M:%S')}\n")
            f.write(f"Total de páginas convertidas: {len(pages)}\n\n")
            
            # Agrupar por space
            spaces = {}
            for page in pages:
                if page.space_title not in spaces:
                    spaces[page.space_title] = []
                spaces[page.space_title].append(page)
            
            f.write("PÁGINAS POR SPACE:\n")
            f.write("-" * 30 + "\n")
            
            for space_title, space_pages in spaces.items():
                f.write(f"\n{space_title} ({len(space_pages)} páginas):\n")
                for page in space_pages:
                    pdf_name = os.path.basename(page.pdf_path) if page.pdf_path else "ERRO"
                    f.write(f"  - {page.title} → {pdf_name}\n")
        
        print(f"📋 Relatório salvo em: {report_path}")
        return report_path


def main():
    """Função principal"""
    
    # CONFIGURAÇÃO - Substituir pelo seu token
    API_TOKEN = "gb_api_Itu4jxLl1CYfAAWsanBq8a5eR3Z16oiTKfhYUxEf" 
    
    if API_TOKEN == "seu_token_aqui":
        print("❌ Por favor, configure seu API_TOKEN")
        return
    
    # Inicializar processador
    processor = SimpleGitBookToPDFProcessor(API_TOKEN, output_dir="gitbook_pdfs")
    
    # Testar conexão
    if not processor.test_connection():
        print("❌ Falha na conexão com GitBook")
        return
    
    # Processar todos os spaces
    converted_pages = processor.process_all_spaces()
    
    # Gerar relatório
    if converted_pages:
        processor.generate_summary_report(converted_pages)
        print(f"\n✅ {len(converted_pages)} PDFs prontos para processamento de embedding!")
        print(f"📁 Pasta: {os.path.abspath(processor.output_dir)}")
        print("\n🔄 PRÓXIMO PASSO: Use seu pipeline existente para processar esses PDFs!")
    else:
        print("❌ Nenhuma página foi convertida")


if __name__ == "__main__":
    main()