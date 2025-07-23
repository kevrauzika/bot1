# pipeline-ingestao/update_index_from_wiki.py

import os
import re
from dotenv import load_dotenv
from azure.core.credentials import AzureKeyCredential
from azure.ai.open_ai import OpenAIClient
from azure.search.documents import SearchClient
from azure.search.documents.models import IndexDocumentsAction
from msrest.authentication import BasicAuthentication
from azure.devops.connection import Connection

# --- Carregar Variáveis de Ambiente ---
print("Carregando variáveis de ambiente...")
load_dotenv()

# Credenciais do Azure DevOps
AZDO_ORG_URL = os.getenv('AZURE_DEVOPS_ORG_URL')
AZDO_PROJECT_NAME = os.getenv('AZURE_DEVOPS_PROJECT_NAME')
AZDO_WIKI_NAME = os.getenv('AZURE_DEVOPS_WIKI_NAME')
AZDO_PAT = os.getenv('AZURE_DEVOPS_PAT')

# Credenciais do Azure AI Search
SEARCH_ENDPOINT = os.getenv('AZURE_AI_SEARCH_ENDPOINT')
SEARCH_API_KEY = os.getenv('AZURE_AI_SEARCH_API_KEY')
SEARCH_INDEX_NAME = os.getenv('AZURE_AI_SEARCH_INDEX_NAME')

# Credenciais do Azure OpenAI
OPENAI_ENDPOINT = os.getenv('AZURE_OPENAI_ENDPOINT')
OPENAI_API_KEY = os.getenv('AZURE_OPENAI_API_KEY')
OPENAI_EMBEDDING_DEPLOYMENT = os.getenv('AZURE_OPENAI_EMBEDDING_DEPLOYMENT')

# --- Funções ---

def fetch_wiki_content():
    """Busca e retorna o conteúdo de texto do Azure DevOps Wiki."""
    print("Conectando ao Azure DevOps para buscar conteúdo do Wiki...")
    try:
        credentials = BasicAuthentication('', AZDO_PAT)
        connection = Connection(base_url=AZDO_ORG_URL, creds=credentials)
        wiki_client = connection.clients.get_wiki_client()
        page = wiki_client.get_page(
            project=AZDO_PROJECT_NAME,
            wiki_identifier=AZDO_WIKI_NAME,
            path='/',  # Página raiz
            include_content=True
        )
        print("Conteúdo do Wiki lido com sucesso.")
        # Remove tags HTML para ter texto limpo
        clean_text = re.sub('<[^<]+?>', '', page.content)
        return clean_text
    except Exception as e:
        print(f"Erro ao buscar conteúdo do Wiki: {e}")
        raise

def chunk_text(text, max_chunk_size=1000):
    """Divide o texto em pedaços (chunks) menores."""
    print(f"Dividindo o texto em chunks de até {max_chunk_size} caracteres.")
    words = text.split()
    chunks = []
    current_chunk = []
    current_length = 0
    for word in words:
        if current_length + len(word) + 1 > max_chunk_size:
            chunks.append(" ".join(current_chunk))
            current_chunk = []
            current_length = 0
        current_chunk.append(word)
        current_length += len(word) + 1
    if current_chunk:
        chunks.append(" ".join(current_chunk))
    print(f"Texto dividido em {len(chunks)} chunks.")
    return chunks

def generate_embeddings(chunks, openai_client):
    """Gera embeddings para uma lista de chunks de texto."""
    print(f"Gerando embeddings para {len(chunks)} chunks de texto...")
    embeddings = []
    for chunk in chunks:
        response = openai_client.embeddings.create(
            input=chunk,
            model=OPENAI_EMBEDDING_DEPLOYMENT
        )
        embeddings.append(response.data[0].embedding)
    print("Embeddings gerados com sucesso.")
    return embeddings

def upload_to_search_index(chunks, embeddings, search_client):
    """Envia os chunks e seus embeddings para o Azure AI Search."""
    print(f"Enviando {len(chunks)} documentos para o índice '{SEARCH_INDEX_NAME}'...")
    documents = []
    for i, chunk in enumerate(chunks):
        documents.append({
            "id": f"wiki_chunk_{i}", # ID único para cada chunk
            "content": chunk,
            "content_vector": embeddings[i],
            "source": f"Azure DevOps Wiki - Chunk {i+1}"
        })

    # Divide em lotes para não sobrecarregar a API
    batch_size = 1000
    for i in range(0, len(documents), batch_size):
        batch = documents[i:i+batch_size]
        search_client.upload_documents(documents=batch)
    
    print("Documentos enviados com sucesso para o Azure AI Search.")


# --- Execução Principal ---
if __name__ == "__main__":
    # 1. Obter o conteúdo do Wiki
    wiki_text = fetch_wiki_content()
    
    # 2. Dividir o conteúdo em chunks
    text_chunks = chunk_text(wiki_text)
    
    # 3. Inicializar os clientes do Azure
    openai_client = OpenAIClient(endpoint=OPENAI_ENDPOINT, api_key=OPENAI_API_KEY)
    search_client = SearchClient(endpoint=SEARCH_ENDPOINT, index_name=SEARCH_INDEX_NAME, credential=AzureKeyCredential(SEARCH_API_KEY))
    
    # 4. Gerar embeddings para cada chunk
    chunk_embeddings = generate_embeddings(text_chunks, openai_client)
    
    # 5. Fazer o upload dos dados para o Azure AI Search
    upload_to_search_index(text_chunks, chunk_embeddings, search_client)
    
    print("\nProcesso de atualização da base de conhecimento concluído com sucesso!")