from fastapi import FastAPI, UploadFile, File, HTTPException
import pdfplumber
import re
import json
from thefuzz import fuzz, process

app = FastAPI(title="Motor de Extração de Notas Fiscais - Local")

# 🎯 DICIONÁRIO CENTRALIZADO NO PYTHON (Fácil de manter e expandir)
MAPA_CATEGORIAS = {
    "Mercearia": ["FEIJAO", "FEIJÃO", "ARROZ", "OLEO", "ÓLEO", "SOYA", "AZEITE", "ACUCAR", "AÇÚCAR", "CAFE", "CAFÉ", "3CORACOES", "CAPS", "3C", "CAPPUCCINO", "CHA", "CHÁ", "LEAO", "ATUM", "MOLHO", "KISABOR", "EKMA", "KICALDO", "CAMIL", "SAL", "MACARRÃO", "MAC.", "D.BENTA", "RAV", "MEZZ", "4QJOS", "MIX SA", "GRANO", "AZEITOVA"],
    "Laticínios e Frios": ["LEITE", "NINHO", "MUSSARELA", "MUSS.", "QUEIJO", "QJOS", "QJO", "DANONE", "IOG", "IOGURTE", "RICOTA", "APRESUNTADO", "AURORA", "BATAV", "FERM", "ELEGE", "P QJ", "DUDUXO", "MASSA LEVE", "REQUEIJAO", "MANTEIGA", "PRESUNTO", "MORTADELA", "SALAME", "PEITO PERU"],    
    "Snacks e Doces": ["BISCOITO", "BISC.", "CLUB SOCIAL", "BOMBOM", "LACTA", "SALG.", "ELMA", "CHIPS", "AMEND", "DOCE", "CRACKER", "RANCHEIRO", "VIVALE", "CHOCOLATE", "CHOC", "GAROTO", "NESTLE", "PASSATEMPO", "WAFER", "BAUDUCCO", "BALA", "FINI", "PIPOCA", "DORITOS", "RUFFLES", "CHEETOS", "PACOCA"],
    "Limpeza": ["OMO", "VANISH", "DETERG", "DET.PO", "AMAC ROUPA", "AMACITEL", "DESINF", "SANOL", "SAC ASSA", "SAC INST.BIO", "60X70", "ESPONJA", "SCOTCH", "BRITE", "PAPEL", "TOALHA", "BULNEZ", "GLADE", "LIMP.PERFUMADO"],
    "Higiene e Saúde": ["SAB.", "DOVE", "REXONA", "DESOD.", "DES.", "COLGATE", "CREME DENT", "CR.D.", "SHAMP", "KARITE", "AERO", "T.MINT"],
    
    # Suplementos captura os itens fitness e os minerais/vitaminas de farmácia
    "Suplementos": ["WHEY", "CREATINA", "BWELL", "MAG", "500MG", "PROTEIN", "GROWTH", "VITAMINA"],
    
    # Farmácia focada estritamente em medicamentos e tratamentos
    "Farmácia": ["NASOAR", "ENVELO", "FRASC", "REMEDIO", "REMEDIOS", "REM.", "MEDICAMENTO", "COMPRIMIDO", "COMP", "CAPSULA", "DIPIRONA", "PARACETAMOL", "IBUPROFENO", "NEOSALADINA", "DORFLEX", "NEOSALDINA", "POMADA", "FARMACIA", "DROGARIA", "BAND-AID", "GAZE", "ALCOOL 70"],
    
    "Hortifruti": ["MAND", "SUGUIM", "PEPINO", "ABOBORA", "ABÓBORA", "ITALIANA", "BATATA", "LAVADA", "ALHO", "CEBOLA", "TOMATE", "BANANA", "MACA", "MAÇÃ", "ALFACE", "CENOURA","MIX SA RUS GRANO 1kg"],
    "Snacks e Doces": ["BISCOITO", "BISC.", "CLUB SOCIAL", "BOMBOM", "LACTA", "SALG.", "ELMA", "AMEND", "DOCE", "CRACKER", "RANCHEIRO", "VIVALE", "CHOCOLATE","SALG.ELMA CHIPS"],
    "Padaria": ["PAO", "PÃO", "PULLMAN", "BISNAGUITO", "KIM"],
    "Bebidas": ["REF.", "SPRITE", "COCO", "PURO COCO", "AGUA", "ÁGUA", "CRYSTAL", "SUCO", "CERVEJA", "VINHO"],
    "Pet Shop": ["RACAO", "RAÇÃO", "PEDIGREE", "DOG", "CAT"],
    
    # Novas categorias mapeadas para o processador Python
    "Automóvel": ["GASOLINA", "ALCOOL", "DIESEL", "SHELL", "IPIRANGA", "COMBUSTIVEL", "POSTO", "MECANICO", "OLEO MOTOR"],
    "Estacionamento": ["ESTACIONAMENTO", "ZONA AZUL", "VALETE", "PARKING", "STOP BANK"],
    "Lazer": ["IFOOD", "BURGER", "MC DONALDS", "PIZZA", "CINEMA", "SHOW", "BAR", "RESTAURANTE", "OUTBACK"],
    "Educação": ["FACULDADE", "FAAP", "CURSO", "UDEMY", "LIVRO", "MATRICULA"],
    "Roupas": ["ROUPA", "CAMISA", "TENIS", "TÊNIS", "SAPATO", "CALCA", "CALÇA", "CASACO", "RENNER", "ZARA", "NIKE", "ADIDAS"]
}

def normalizar_valor(valor_str):
    if not valor_str:
        return 0.0
    valor_limpo = valor_str.replace(',', '.')
    try:
        return float(valor_limpo)
    except ValueError:
        return 0.0

def classificar_produto_local(nome_produto):
    nome_alvo = nome_produto.upper()
    melhor_categoria = "Outros"
    maior_score = 0

    # Varre nosso dicionário calculando a proximidade matemática das palavras
    for categoria, palavras_chave in MAPA_CATEGORIAS.items():
        for keyword in palavras_chave:
            # Se a palavra exata estiver no nome, o match é perfeito (Score 100)
            if keyword in nome_alvo:
                return categoria
            
            # Fuzzy match para pegar variações ou abreviações do mercado
            score = fuzz.partial_ratio(keyword, nome_alvo)
            if score > maior_score and score > 80:  # Margem de segurança de 80%
                maior_score = score
                melhor_categoria = categoria

    return melhor_categoria

@app.post("/extrair-nota")
async def extrair_nota(file: UploadFile = File(...)):
    print(f"\n[PYTHON] ==== NOVA REQUISIÇÃO (CATEGORIZAÇÃO LOCAL): {file.filename} ====")
    
    if not file.filename.lower().endswith('.pdf'):
        raise HTTPException(status_code=400, detail="O arquivo enviado precisa ser um PDF.")

    itens_extraidos = []
    estabelecimento = "NOTA FISCAL"
    
    try:
        with pdfplumber.open(file.file) as pdf:
            texto_completo = ""
            for pagina in pdf.pages:
                texto_completo += pagina.extract_text() or ""

            linhas = texto_completo.split('\n')
            print(f"[PYTHON] Total de linhas brutas identificadas: {len(linhas)}")

            texto_caps = texto_completo.upper()
            if "WMS SUPERMERCADOS" in texto_caps or "SONDA" in texto_caps or "WALMART" in texto_caps:
                estabelecimento = "WMS SUPERMERCADOS (SONDA/WALMART)"
            elif "SENDAS" in texto_caps or "ASSAI" in texto_caps or "ASSAÍ" in texto_caps:
                estabelecimento = "SENDAS DISTRIBUIDORA (ASSAI)"
            elif "RAIADROGASIL" in texto_caps or "DROGASIL" in texto_caps:
                estabelecimento = "RAIADROGASIL S.A."

            nome_pendente = None
            for linha in linhas:
                linha_str = linha.strip()
                
                if re.search(r'\(C[oó]digo:', linha_str, re.IGNORECASE):
                    idx_codigo = re.search(r'\(C[oó]digo:', linha_str, re.IGNORECASE)
                    nome = linha_str[:idx_codigo.start()]
                    nome = re.sub(r'\b(Qtde|UN|VI|Unit|Total|CNPJ|VALOR|PAGO|CONSUMIDOR)\b.*', '', nome, flags=re.IGNORECASE)
                    nome = re.sub(r'^[,\.\-\/X\) \(\d+]+|[,\.\-\/X\) \(\d+]+$', '', nome).strip()
                    nome = nome.replace('"', '').replace("'", "")
                    
                    if len(nome) > 2:
                        nome_pendente = nome
                    continue

                if nome_pendente:
                    match_preco = re.search(r'Unit?\.?:?\s*([\d\.,]+)', linha_str, re.IGNORECASE)
                    match_qtd = re.search(r'Qtde?\.?:?\s*([\d\.,]+)', linha_str, re.IGNORECASE)

                    if match_preco:
                        preco = normalizar_valor(match_preco.group(1))
                        qtd = normalizar_valor(match_qtd.group(1)) if match_qtd else 1.0

                        if preco > 0:
                            # 🎯 Aplica a nossa classificação local rápida baseada em inteligência estatística
                            categoria_detectada = classificar_produto_local(nome_pendente)
                            
                            itens_extraidos.append({
                                "descricao": nome_pendente,
                                "quantidade": qtd,
                                "preco": preco,
                                "categoria": categoria_detectada
                            })
                            print(f"   -> [LOCAL MATCH] {nome_pendente} ===> {categoria_detectada}")
                        nome_pendente = None

        print(f"[PYTHON] Processamento concluído. {len(itens_extraidos)} itens processados com sucesso.")
        return {
            "estabelecimento": estabelecimento if 'establishment' in locals() else estabelecimento,
            "textoBruto": texto_completo,
            "itens": itens_extraidos
        }

    except Exception as e:
        print(f"[ERRO CRÍTICO PYTHON] Houve uma falha no processamento: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Erro interno no extrator Python: {str(e)}")