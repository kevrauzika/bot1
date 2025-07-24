#!/usr/bin/env node

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
    CallToolRequestSchema,
    ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import dotenv from 'dotenv';

// Importar ferramentas
import { wikiSearchTool, searchWikiPages } from './tools/wiki-search.js';
import { fileOperationsTool, readFile, writeFile } from './tools/file-operations.js';
import { devopsIntegrationTool, getWorkItems, createWorkItem } from './tools/devops-integration.js';

dotenv.config();

const server = new Server(
    {
        name: 'chatbot-mcp-server',
        version: '1.0.0',
    },
    {
        capabilities: {
            tools: {},
        },
    }
);

// Lista de ferramentas disponíveis
server.setRequestHandler(ListToolsRequestSchema, async () => {
    return {
        tools: [
            wikiSearchTool,
            fileOperationsTool,
            devopsIntegrationTool,
            {
                name: 'get_system_info',
                description: 'Obtém informações do sistema e status do bot',
                inputSchema: {
                    type: 'object',
                    properties: {},
                },
            },
        ],
    };
});

// Handler para execução de ferramentas
server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    try {
        switch (name) {
            case 'wiki_search':
                const searchResults = await searchWikiPages(args.query, args.limit || 5);
                return {
                    content: [
                        {
                            type: 'text',
                            text: JSON.stringify(searchResults, null, 2),
                        },
                    ],
                };

            case 'file_operations':
                if (args.operation === 'read') {
                    const content = await readFile(args.path);
                    return {
                        content: [
                            {
                                type: 'text',
                                text: content,
                            },
                        ],
                    };
                } else if (args.operation === 'write') {
                    await writeFile(args.path, args.content);
                    return {
                        content: [
                            {
                                type: 'text',
                                text: `Arquivo ${args.path} criado/atualizado com sucesso`,
                            },
                        ],
                    };
                }
                break;

            case 'devops_integration':
                if (args.action === 'get_work_items') {
                    const workItems = await getWorkItems(args.query);
                    return {
                        content: [
                            {
                                type: 'text',
                                text: JSON.stringify(workItems, null, 2),
                            },
                        ],
                    };
                } else if (args.action === 'create_work_item') {
                    const newItem = await createWorkItem(args.title, args.description, args.type);
                    return {
                        content: [
                            {
                                type: 'text',
                                text: `Work item criado: ${JSON.stringify(newItem, null, 2)}`,
                            },
                        ],
                    };
                }
                break;

            case 'get_system_info':
                return {
                    content: [
                        {
                            type: 'text',
                            text: JSON.stringify({
                                server: 'chatbot-mcp-server',
                                version: '1.0.0',
                                uptime: process.uptime(),
                                memory: process.memoryUsage(),
                                timestamp: new Date().toISOString(),
                            }, null, 2),
                        },
                    ],
                };

            default:
                throw new Error(`Ferramenta desconhecida: ${name}`);
        }
    } catch (error) {
        return {
            content: [
                {
                    type: 'text',
                    text: `Erro ao executar ${name}: ${error.message}`,
                },
            ],
            isError: true,
        };
    }
});

// Inicializar servidor
async function main() {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error('Chatbot MCP Server rodando na porta stdio');
}

main().catch((error) => {
    console.error('Erro ao iniciar servidor MCP:', error);
    process.exit(1);
});