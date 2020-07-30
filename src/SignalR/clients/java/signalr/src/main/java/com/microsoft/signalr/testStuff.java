package com.microsoft.signalr;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Base64;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import org.msgpack.core.MessageInsufficientBufferException;
import org.msgpack.core.MessagePack;
import org.msgpack.core.MessagePackException;
import org.msgpack.core.MessagePacker;
import org.msgpack.core.MessageUnpacker;

public class testStuff {
	
	public TestBinder binder;
	
	public testStuff(HubMessage hm) {
		binder = new TestBinder(hm);
	}

	public static void main(String[] args) throws IOException {
		ByteArrayOutputStream out = new ByteArrayOutputStream();
    	MessagePacker packer = MessagePack.newDefaultPacker(out);
		
		MessagePackHubProtocol prot = new MessagePackHubProtocol();
		
		System.out.println(prot.getName());
		System.out.println(prot.getVersion());
		System.out.println(prot.getTransferFormat());
		
		Map<String, String> headers = new HashMap<String, String>();
		headers.put("key", "value");
		
		Map<String, List<String>> map = new HashMap<String, List<String>>();
		List<String> list = new ArrayList<String>();
		list.add("abc");
		map.put("ding", list);
        InvocationMessage invocationMessage = new InvocationMessage(null, "1", "test", new Object[] {42, map}, null);
        StreamItem streamItem = new StreamItem(headers, "test", 69);
        		
        String result = prot.writeMessage(invocationMessage);
        String itemResult = prot.writeMessage(streamItem);
        byte[] bytes = result.getBytes(StandardCharsets.ISO_8859_1);
        for (byte b: bytes) {
        	System.out.printf("0x%02X ", b);
        }
        System.out.println();
        byte[] itemBytes = itemResult.getBytes(StandardCharsets.ISO_8859_1);
        for (byte b: itemBytes) {
        	System.out.printf("0x%02X ", b);
        }
        System.out.println();
        
        testStuff ts = new testStuff(invocationMessage);
        
        HubMessage[] messages = prot.parseMessages(result + itemResult, ts.binder);
        
        InvocationMessage im = (InvocationMessage) messages[0];
        System.out.println(im.getInvocationId());
        System.out.println(im.getTarget());
        System.out.println(im.getArguments()[0]);
        System.out.println(im.getArguments()[1]);
        System.out.println(im.getHeaders());
        System.out.println(im.getStreamIds());
        
        System.out.println();
        StreamItem sm = (StreamItem) messages[1];
        System.out.println(sm.getInvocationId());
        System.out.println(sm.getItem());
        System.out.println(sm.getHeaders());
        
        System.out.println();
        
        
	}
	
	public static Object get(int i) {
		if (i % 4 == 0) {
			return 2;
		} if (i % 4 == 1) {
			return false;
		} else if (i % 4 == 2) {
			return "butter";
		} else {
			List<Object> objects = new ArrayList<Object>();
			objects.add(get(2));
			return objects;
		}
	}
	
    private class TestBinder implements InvocationBinder {
        private Class<?>[] paramTypes = null;
        private Class<?> returnType = Integer.class;

        public TestBinder(HubMessage expectedMessage) {
            if (expectedMessage == null) {
                return;
            }

            switch (expectedMessage.getMessageType()) {
                case STREAM_INVOCATION:
                    ArrayList<Class<?>> streamTypes = new ArrayList<>();
                    for (Object obj : ((StreamInvocationMessage) expectedMessage).getArguments()) {
                        streamTypes.add(obj.getClass());
                    }
                    paramTypes = streamTypes.toArray(new Class<?>[streamTypes.size()]);
                    break;
                case INVOCATION:
                    ArrayList<Class<?>> types = new ArrayList<>();
                    for (Object obj : ((InvocationMessage) expectedMessage).getArguments()) {
                        types.add(obj.getClass());
                    }
                    paramTypes = types.toArray(new Class<?>[types.size()]);
                    break;
                case STREAM_ITEM:
                    break;
                case COMPLETION:
                    returnType = ((CompletionMessage)expectedMessage).getResult().getClass();
                    break;
                default:
                    break;
            }
        }

        @Override
        public Class<?> getReturnType(String invocationId) {
            return returnType;
        }

        @Override
        public List<Class<?>> getParameterTypes(String methodName) {
            if (paramTypes == null) {
                return new ArrayList<>();
            }
            return new ArrayList<Class<?>>(Arrays.asList(paramTypes));
        }
    }
}
